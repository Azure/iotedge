use crate::{API_VERSION, UrlConnector};
use crate::error::{Error, ErrorKind};

use std::sync::Arc;

use aziot_secret::apis::client::APIClient;
use aziot_secret::apis::configuration::Configuration;
use aziot_secret::apis::{ApiError, Error as SecretError};
use edgelet_core::{SecretManager, UrlExt};
use failure::ResultExt;
use futures::Future;
use hyper::Client;
use url::Url;

#[derive(Clone)]
pub struct SecretClient {
    client: Arc<APIClient>
}

impl SecretClient {
    pub fn new(url: &Url) -> Result<Self, Error> {
        let client = Client::builder()
            .build(UrlConnector::new(url).context(ErrorKind::Initialization)?);

        let scheme = url.scheme();
        let base_path = url
            .to_base_path()
            .context(ErrorKind::Initialization)?
            .to_str()
            .ok_or(ErrorKind::Initialization)?
            .to_owned();
        let mut configuration = Configuration::new(client);
        configuration.base_path = UrlConnector::build_hyper_uri(
                scheme,
                &base_path,
                "/"
            )
            .context(ErrorKind::Initialization)?
            .to_string();

        let secret_client = Self {
            client: Arc::new(APIClient::new(configuration)),
        };
        Ok(secret_client)
    }
}

// NOTE: potentially split into workload and management and move to edgelet-http
//       VERY LOW PRIORITY
impl SecretManager for SecretClient {
    type Error = Error;
    type SetFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type DeleteFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type GetFuture = Box<dyn Future<Item = String, Error = Self::Error> + Send>;
    type PullFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type RefreshFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;

    fn set(&self, id: &str, value: &str) -> Self::SetFuture {
        let response = self.client
            .default_api()
            .set_secret(&API_VERSION.to_string(), &id, &value)
            .map_err(|e| {
                if let SecretError::ApiError(ApiError { code, .. }) = e {
                    Error::from(ErrorKind::SecretStore(code.as_u16()))
                }
                else {
                    Error::from(ErrorKind::SecretStore(500))
                }
            });

        Box::new(response)
    }

    fn delete(&self, id: &str) -> Self::DeleteFuture {
        let response = self.client
            .default_api()
            .delete_secret(&API_VERSION.to_string(), id)
            .map_err(|e| {
                if let SecretError::ApiError(ApiError { code, .. }) = e {
                    Error::from(ErrorKind::SecretStore(code.as_u16()))
                }
                else {
                    Error::from(ErrorKind::SecretStore(500))
                }
            });

        Box::new(response)
    }

    fn get(&self, id: &str) -> Self::GetFuture {
        let response = self.client
            .default_api()
            .get_secret(&API_VERSION.to_string(), id)
            .map_err(|e| {
                if let SecretError::ApiError(ApiError { code, .. }) = e {
                    Error::from(ErrorKind::SecretStore(code.as_u16()))
                }
                else {
                    Error::from(ErrorKind::SecretStore(500))
                }
            });

        Box::new(response)
    }

    fn pull(&self, id: &str, akv_id: &str) -> Self::PullFuture {
        let response = self.client
            .default_api()
            .pull_secret(&API_VERSION.to_string(), id, akv_id)
            .map_err(|e| {
                if let SecretError::ApiError(ApiError { code, .. }) = e {
                    Error::from(ErrorKind::SecretStore(code.as_u16()))
                }
                else {
                    Error::from(ErrorKind::SecretStore(500))
                }
            });

        Box::new(response)
    }

    fn refresh(&self, id: &str) -> Self::RefreshFuture {
        let response = self.client
            .default_api()
            .refresh_secret(&API_VERSION.to_string(), id)
            .map_err(|e| {
                if let SecretError::ApiError(ApiError { code, .. }) = e {
                    Error::from(ErrorKind::SecretStore(code.as_u16()))
                }
                else {
                    Error::from(ErrorKind::SecretStore(500))
                }
            });

        Box::new(response)
    }
}
