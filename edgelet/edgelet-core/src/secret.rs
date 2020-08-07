use std::fmt;

use failure::Fail;
use futures::Future;

pub trait SecretManager {
    type Error: Fail;
    type SetFuture: Future<Item = (), Error = Self::Error> + Send;
    type DeleteFuture: Future<Item = (), Error = Self::Error> + Send;
    type GetFuture: Future<Item = String, Error = Self::Error> + Send;
    type PullFuture: Future<Item = (), Error = Self::Error> + Send;
    type RefreshFuture: Future<Item = (), Error = Self::Error> + Send;
    
    fn set(&self, id: &str, value: &str) -> Self::SetFuture;
    fn delete(&self, id: &str) -> Self::DeleteFuture;
    fn get(&self, id: &str) -> Self::GetFuture;
    fn pull(&self, id: &str, akv_id: &str) -> Self::PullFuture;
    fn refresh(&self, id: &str) -> Self::RefreshFuture;
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
