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

    type DeviceProvisioningInformationFuture: Future<
            Item = DeviceProvisioningInfo,
            Error = Self::Error,
        > + Send;

    fn get_device_provisioning_information(&self) -> Self::DeviceProvisioningInformationFuture;

    fn reprovision_device(&self) -> Self::DeviceProvisioningInformationFuture;
}

pub trait GetApi: Send + Sync {
    fn get_api(&self) -> &dyn ExternalProvisioningApi;
}

#[derive(Clone)]
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

impl ExternalProvisioningInterface for ExternalProvisioningClient {
    type Error = Error;

    type DeviceProvisioningInformationFuture =
        Box<dyn Future<Item = DeviceProvisioningInfo, Error = Self::Error> + Send>;

    fn get_device_provisioning_information(&self) -> Self::DeviceProvisioningInformationFuture {
        let connection_info = self
            .client
            .get_api()
            .get_device_provisioning_information(crate::EXTERNAL_PROVISIONING_API_VERSION)
            .map_err(|err| {
                Error::from_external_provisioning_error(
                    err,
                    ErrorKind::GetDeviceProvisioningInformation,
                )
            });
        Box::new(connection_info)
    }

    fn reprovision_device(&self) -> Self::DeviceProvisioningInformationFuture {
        let connection_info = self
            .client
            .get_api()
            .reprovision_device(crate::EXTERNAL_PROVISIONING_API_VERSION)
            .map_err(|err| {
                Error::from_external_provisioning_error(err, ErrorKind::ReprovisionDevice)
            });
        Box::new(connection_info)
    }
}

#[cfg(test)]
mod tests {
    use std::mem::discriminant;

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
        fn get_device_provisioning_information(
            &self,
            _api_version: &str,
        ) -> Box<
            dyn Future<
                    Item = external_provisioning::models::DeviceProvisioningInfo,
                    Error = ExternalProvisioningError<serde_json::Value>,
                > + Send,
        > {
            match self.error.as_ref() {
                None => {
                    let mut credentials =
                        Credentials::new("symmetric-key".to_string(), "payload".to_string());
                    credentials.set_key("test-key".to_string());
                    let provisioning_info = DeviceProvisioningInfo::new(
                        "TestHub".to_string(),
                        "TestDevice".to_string(),
                        credentials,
                    );

                    Box::new(Ok(provisioning_info).into_future())
                }
                Some(s) => Box::new(Err(ExternalProvisioningError::Api(s.clone().0)).into_future()),
            }
        }

        fn reprovision_device(
            &self,
            _api_version: &str,
        ) -> Box<
            dyn Future<
                    Item = external_provisioning::models::DeviceProvisioningInfo,
                    Error = ExternalProvisioningError<serde_json::Value>,
                > + Send,
        > {
            match self.error.as_ref() {
                None => {
                    let mut credentials =
                        Credentials::new("symmetric-key".to_string(), "payload".to_string());
                    credentials.set_key("test-key".to_string());
                    let provisioning_info = DeviceProvisioningInfo::new(
                        "TestHub".to_string(),
                        "TestDevice".to_string(),
                        credentials,
                    );

                    Box::new(Ok(provisioning_info).into_future())
                }
                Some(s) => Box::new(Err(ExternalProvisioningError::Api(s.clone().0)).into_future()),
            }
        }
    }

    #[test]
    fn get_device_provisioning_info_error() {
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

        provisioning_info_test_assert(
            Some(ErrorKind::GetDeviceProvisioningInformation),
            move || {
                client
                    .get_device_provisioning_information()
                    .then(|result| result)
                    .wait()
            },
        );
    }

    #[test]
    fn get_device_provisioning_info_success() {
        let external_provisioning_api = TestExternalProvisioningApi { error: None };
        let client = ExternalProvisioningClient {
            client: Arc::new(external_provisioning_api),
        };

        provisioning_info_test_assert(None, move || {
            client
                .get_device_provisioning_information()
                .then(|result| result)
                .wait()
        });
    }

    #[test]
    fn reprovision_device_error() {
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

        provisioning_info_test_assert(Some(ErrorKind::ReprovisionDevice), move || {
            client.reprovision_device().then(|result| result).wait()
        });
    }

    #[test]
    fn reprovision_device_success() {
        let external_provisioning_api = TestExternalProvisioningApi { error: None };
        let client = ExternalProvisioningClient {
            client: Arc::new(external_provisioning_api),
        };

        provisioning_info_test_assert(None, move || {
            client.reprovision_device().then(|result| result).wait()
        });
    }

    pub fn provisioning_info_test_assert<F>(error_kind: Option<ErrorKind>, executor: F)
    where
        F: FnOnce() -> Result<DeviceProvisioningInfo, Error> + Sync + Send + 'static,
    {
        let result = executor();
        let res = if let Some(error_kind_val) = error_kind {
            match result {
                Ok(_) => panic!("Expected a failure."),
                Err(err) => {
                    if discriminant(err.kind()) == discriminant(&error_kind_val) {
                        Ok::<_, Error>(())
                    } else {
                        panic!("Expected `{}` but got {:?}", error_kind_val, err)
                    }
                }
            }
            .is_ok()
        } else {
            match result {
                Ok(item) => {
                    assert_eq!("TestHub", item.hub_name());
                    assert_eq!("TestDevice", item.device_id());
                    assert_eq!("symmetric-key", item.credentials().auth_type());
                    assert_eq!("payload", item.credentials().source());

                    if let Some(key) = item.credentials().key() {
                        assert_eq!(key, "test-key");
                    } else {
                        panic!("A key was expected in the response.")
                    }

                    Ok::<_, Error>(())
                }
                Err(_err) => panic!("Did not expect a failure."),
            }
            .is_ok()
        };

        assert!(res);
    }
}
