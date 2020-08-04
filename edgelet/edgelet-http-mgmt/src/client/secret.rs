use crate::error::{Error, ErrorKind};

use std::fmt;
use std::sync::Arc;

use edgelet_core::SecretManager;
use edgelet_http::{API_VERSION, IntoResponse};
use futures::Future;
use secret_store::apis::client::APIClient;
use secret_store::apis::{ApiError, Error as SecretError};

pub struct SecretClient {
    client: Arc<APIClient>
}

impl SecretClient {
    pub fn new(client: APIClient) -> Self {
        Self {
            client: Arc::new(client)
        }
    }
}

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

#[derive(Clone, Debug)]
pub enum SecretOperation {
    Set(String),
    Delete(String),
    Get(String),
    Pull(String, String),
    Refresh(String)
}

impl fmt::Display for SecretOperation {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SecretOperation::Set(id) =>
                write!(f, "Could not create secret {}", id),
            SecretOperation::Delete(id) =>
                write!(f, "Could not delete secret {}", id),
            SecretOperation::Get(id) =>
                write!(f, "Could not get secret {}", id),
            SecretOperation::Pull(id, uri) =>
                write!(f, "Could not pull secret {} from {}", id, uri),
            SecretOperation::Refresh(id) =>
                write!(f, "Could not refresh secret {}", id)
        }
    }
}
