use std::fmt;

use failure::Fail;
use futures::Future;

pub trait SecretManager {
    type Error: Fail;
    type CreateFuture: Future<Item = (), Error = Self::Error> + Send;
    type DeleteFuture: Future<Item = (), Error = Self::Error> + Send;
    type GetFuture: Future<Item = String, Error = Self::Error> + Send;
    type PullFuture: Future<Item = (), Error = Self::Error> + Send;
    type RefreshFuture: Future<Item = (), Error = Self::Error> + Send;
    
    fn create(&mut self, id: String, value: String) -> Self::CreateFuture;
    fn delete(&mut self, id: String) -> Self::DeleteFuture;
    fn get(&mut self, id: String) -> Self::GetFuture;
    fn pull(&mut self, id: String, akv_id: String) -> Self::PullFuture;
    fn refresh(&mut self, id: String) -> Self::RefreshFuture;
}

#[derive(Clone, Debug)]
pub enum SecretOperation {
    CreateSecret(String),
    DeleteSecret(String),
    GetSecret(String),
    PullSecret(String, String),
    RefreshSecret(String)
}

impl fmt::Display for SecretOperation {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SecretOperation::CreateSecret(id) =>
                write!(f, "Could not create secret {}", id),
            SecretOperation::DeleteSecret(id) =>
                write!(f, "Could not delete secret {}", id),
            SecretOperation::GetSecret(id) =>
                write!(f, "Could not get secret {}", id),
            SecretOperation::PullSecret(id, uri) =>
                write!(f, "Could not pull secret {} from {}", id, uri),
            SecretOperation::RefreshSecret(id) =>
                write!(f, "Could not refresh secret {}", id)
        }
    }
}