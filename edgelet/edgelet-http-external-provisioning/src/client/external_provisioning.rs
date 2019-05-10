// Copyright (c) Microsoft. All rights reserved.

use std::sync::Arc;

use external_provisioning::apis::client::APIClient;
use external_provisioning::apis::configuration::Configuration;
use external_provisioning::apis::ExternalProvisioningApi;
use external_provisioning::models::*;
use failure::{Fail, ResultExt};
use futures::prelude::*;
use hyper::Client;
use url::Url;

use edgelet_core::UrlExt;
use edgelet_http::UrlConnector;

use crate::error::{Error, ErrorKind};

pub trait ExternalProvisioningInterface {
    type Error: Fail;

    type DeviceConnectionInformationFuture: Future<Item = DeviceConnectionInfo, Error = Self::Error>
        + Send;

    fn get_device_connection_information(&self) -> Self::DeviceConnectionInformationFuture;
}

pub trait GetApi {
    fn get_api(&self) -> &dyn ExternalProvisioningApi;
}

pub struct ExternalProvisioningClient {
    client: Arc<dyn GetApi>,
}

impl GetApi for APIClient {
    fn get_api(&self) -> &dyn ExternalProvisioningApi {
        self.external_provisioning_api()
    }
}

impl ExternalProvisioningClient {
    pub fn new(url: &Url) -> Result<Self, Error> {
        let client = Client::builder().build(
            UrlConnector::new(url).context(ErrorKind::InitializeExternalProvisioningClient)?,
        );

        let base_path = url
            .to_base_path()
            .context(ErrorKind::InitializeExternalProvisioningClient)?;
        let mut configuration = Configuration::new(client);
        configuration.base_path = base_path
            .to_str()
            .ok_or(ErrorKind::InitializeExternalProvisioningClient)?
            .to_string();

        let scheme = url.scheme().to_string();
        configuration.uri_composer = Box::new(move |base_path, path| {
            Ok(UrlConnector::build_hyper_uri(&scheme, base_path, path)?)
        });

        let external_provisioning_client = ExternalProvisioningClient {
            client: Arc::new(APIClient::new(configuration)),
        };

        Ok(external_provisioning_client)
    }
}

impl Clone for ExternalProvisioningClient {
    fn clone(&self) -> Self {
        ExternalProvisioningClient {
            client: self.client.clone(),
        }
    }
}

impl ExternalProvisioningInterface for ExternalProvisioningClient {
    type Error = Error;

    type DeviceConnectionInformationFuture =
        Box<dyn Future<Item = DeviceConnectionInfo, Error = Self::Error> + Send>;

    fn get_device_connection_information(&self) -> Self::DeviceConnectionInformationFuture {
        let connection_info = self
            .client
            .get_api()
            .get_device_connection_information(crate::EXTERNAL_PROVISIONING_API_VERSION)
            .map_err(|err| {
                Error::from_external_provisioning_error(
                    err,
                    ErrorKind::GetDeviceConnectionInformation,
                )
            });
        Box::new(connection_info)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use external_provisioning::apis::ApiError as ExternalProvisioningApiError;
    use external_provisioning::apis::Error as ExternalProvisioningError;
    use futures::Future;

    #[test]
    fn invalid_external_provisioning_url() {
        let client = ExternalProvisioningClient::new(&(Url::parse("fd://").unwrap()));
        match client {
            Ok(_t) => panic!("Unexpected to succeed with invalid Url."),
            Err(ref err) => {
                if let ErrorKind::InitializeExternalProvisioningClient = err.kind() {
                } else {
                    panic!(
                        "Expected `InitializeExternalProvisioningClient` but got {:?}",
                        err
                    );
                }
            }
        };
    }

    #[test]
    fn valid_external_provisioning_url() {
        let client =
            ExternalProvisioningClient::new(&(Url::parse("http://localhost:99/").unwrap()));
        assert!(client.is_ok());
    }

    struct TestExternalProvisioningApiError(ExternalProvisioningApiError<serde_json::Value>);

    impl Clone for TestExternalProvisioningApiError {
        fn clone(&self) -> Self {
            Self(ExternalProvisioningApiError {
                code: self.0.code,
                content: self.0.content.clone(),
            })
        }
    }

    struct TestExternalProvisioningApi {
        pub error: Option<TestExternalProvisioningApiError>,
    }

    impl GetApi for TestExternalProvisioningApi {
        fn get_api(&self) -> &dyn ExternalProvisioningApi {
            self
        }
    }

    impl ExternalProvisioningApi for TestExternalProvisioningApi {
        fn get_device_connection_information(
            &self,
            _api_version: &str,
        ) -> Box<
            dyn Future<
                    Item = external_provisioning::models::DeviceConnectionInfo,
                    Error = ExternalProvisioningError<serde_json::Value>,
                > + Send,
        > {
            match self.error.as_ref() {
                None => Box::new(
                    Ok(DeviceConnectionInfo::new(
                        "hub".to_string(),
                        "device".to_string(),
                    ))
                    .into_future(),
                ),
                Some(s) => Box::new(Err(ExternalProvisioningError::Api(s.clone().0)).into_future()),
            }
        }
    }

    #[test]
    fn get_device_connection_info_error() {
        let external_provisioning_error =
            TestExternalProvisioningApiError(ExternalProvisioningApiError {
                code: hyper::StatusCode::from_u16(400).unwrap(),
                content: None,
            });
        let external_provisioning_api = TestExternalProvisioningApi {
            error: Some(external_provisioning_error),
        };
        let client = ExternalProvisioningClient {
            client: Arc::new(external_provisioning_api),
        };

        let res = client
            .get_device_connection_information()
            .then(|result| match result {
                Ok(_) => panic!("Expected a failure."),
                Err(err) => match err.kind() {
                    ErrorKind::GetDeviceConnectionInformation => Ok::<_, Error>(()),
                    _ => panic!(
                        "Expected `GetDeviceConnectionInformation` but got {:?}",
                        err
                    ),
                },
            })
            .wait()
            .is_ok();

        assert!(res);
    }

    #[test]
    fn get_device_connection_info_success() {
        let external_provisioning_api = TestExternalProvisioningApi { error: None };
        let client = ExternalProvisioningClient {
            client: Arc::new(external_provisioning_api),
        };

        let res = client
            .get_device_connection_information()
            .then(|result| match result {
                Ok(item) => {
                    assert_eq!("hub", item.hub_name());
                    assert_eq!("device", item.device_id());
                    Ok::<_, Error>(())
                }
                Err(_err) => panic!("Did not expect a failure."),
            })
            .wait()
            .is_ok();

        assert!(res);
    }
}
