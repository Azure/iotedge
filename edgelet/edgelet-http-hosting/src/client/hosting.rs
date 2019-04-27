// Copyright (c) Microsoft. All rights reserved.

use std::sync::Arc;

use failure::{Fail, ResultExt};
use futures::prelude::*;
use hosting::apis::client::APIClient;
use hosting::apis::configuration::Configuration;
use hosting::apis::HostingApi;
use hosting::models::*;
use hyper::Client;
use url::Url;

use edgelet_core::UrlExt;
use edgelet_http::UrlConnector;

use crate::error::{Error, ErrorKind};

pub trait HostingInterface {
    type Error: Fail;

    type DeviceConnectionInformationFuture: Future<Item = DeviceConnectionInfo, Error = Self::Error>
        + Send;

    fn get_device_connection_information(&self) -> Self::DeviceConnectionInformationFuture;
}

pub trait GetApi {
    fn get_api(&self) -> &dyn HostingApi;
}

pub struct HostingClient {
    client: Arc<dyn GetApi>,
}

impl GetApi for APIClient {
    fn get_api(&self) -> &dyn HostingApi {
        self.hosting_api()
    }
}

impl HostingClient {
    pub fn new(url: &Url) -> Result<Self, Error> {
        let client = Client::builder()
            .build(UrlConnector::new(url).context(ErrorKind::InitializeHostingClient)?);

        let base_path = url
            .to_base_path()
            .context(ErrorKind::InitializeHostingClient)?;
        let mut configuration = Configuration::new(client);
        configuration.base_path = base_path
            .to_str()
            .ok_or(ErrorKind::InitializeHostingClient)?
            .to_string();

        let scheme = url.scheme().to_string();
        configuration.uri_composer = Box::new(move |base_path, path| {
            Ok(UrlConnector::build_hyper_uri(&scheme, base_path, path)?)
        });

        let hosting_client = HostingClient {
            client: Arc::new(APIClient::new(configuration)),
        };

        Ok(hosting_client)
    }
}

impl Clone for HostingClient {
    fn clone(&self) -> Self {
        HostingClient {
            client: self.client.clone(),
        }
    }
}

impl HostingInterface for HostingClient {
    type Error = Error;

    type DeviceConnectionInformationFuture =
        Box<dyn Future<Item = DeviceConnectionInfo, Error = Self::Error> + Send>;

    fn get_device_connection_information(&self) -> Self::DeviceConnectionInformationFuture {
        let connection_info = self
            .client
            .get_api()
            .get_device_connection_information(crate::HOSTING_API_VERSION)
            .map_err(|err| {
                Error::from_hosting_error(err, ErrorKind::GetDeviceConnectionInformation)
            });
        Box::new(connection_info)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use futures::Future;
    use hosting::apis::ApiError as HostingApiError;
    use hosting::apis::Error as HostingError;

    #[test]
    fn invalid_hosting_url() {
        let client = HostingClient::new(&(Url::parse("fd://").unwrap()));
        match client {
            Ok(_t) => panic!("Unexpected to succeed with invalid Url."),
            Err(ref err) => {
                if let ErrorKind::InitializeHostingClient = err.kind() {
                } else {
                    panic!("Expected `InitializeHostingClient` but got {:?}", err);
                }
            }
        };
    }

    #[test]
    fn valid_hosting_url() {
        let client = HostingClient::new(&(Url::parse("http://localhost:99/").unwrap()));
        assert!(client.is_ok());
    }

    struct TestHostingApiError(HostingApiError<serde_json::Value>);

    impl Clone for TestHostingApiError {
        fn clone(&self) -> Self {
            Self(HostingApiError {
                code: self.0.code,
                content: self.0.content.clone(),
            })
        }
    }

    struct TestHostingApi {
        pub error: Option<TestHostingApiError>,
    }

    impl GetApi for TestHostingApi {
        fn get_api(&self) -> &dyn HostingApi {
            self
        }
    }

    impl HostingApi for TestHostingApi {
        fn get_device_connection_information(
            &self,
            _api_version: &str,
        ) -> Box<
            dyn Future<
                    Item = hosting::models::DeviceConnectionInfo,
                    Error = HostingError<serde_json::Value>,
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
                Some(s) => Box::new(Err(HostingError::Api(s.clone().0)).into_future()),
            }
        }
    }

    #[test]
    fn get_device_connection_info_error() {
        let hosting_error = TestHostingApiError(HostingApiError {
            code: hyper::StatusCode::from_u16(400).unwrap(),
            content: None,
        });
        let hosting_api = TestHostingApi {
            error: Some(hosting_error),
        };
        let client = HostingClient {
            client: Arc::new(hosting_api),
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
        let hosting_api = TestHostingApi { error: None };
        let client = HostingClient {
            client: Arc::new(hosting_api),
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
