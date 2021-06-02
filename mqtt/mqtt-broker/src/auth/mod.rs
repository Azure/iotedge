mod authentication;
mod authorization;

pub use authentication::{
    authenticate_fn_ok, AuthenticationContext, Authenticator, Certificate, DefaultAuthenticator,
    DynAuthenticator,
};
pub use authorization::{
    authorize_fn_ok, Activity, AllowAll, Authorization, Authorizer, DenyAll, Operation,
    Publication, Publish, Subscribe,
};

use std::{
    fmt::{Display, Formatter, Result as FmtResult},
    sync::Arc,
};

use serde::{Deserialize, Serialize};

/// Authenticated MQTT client identity.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub enum AuthId {
    /// Identity for anonymous client.
    Anonymous,

    /// Identity for non-anonymous client.
    Identity(Identity),
}

impl AuthId {
    pub fn as_str(&self) -> &str {
        match self {
            AuthId::Anonymous => "*",
            AuthId::Identity(identity) => identity.as_str(),
        }
    }
}

impl Display for AuthId {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        write!(f, "{}", self.as_str())
    }
}

impl AuthId {
    /// Creates a MQTT identity for known client.
    pub fn from_identity<T: Into<Identity>>(identity: T) -> Self {
        Self::Identity(identity.into())
    }
}

impl<T: Into<Identity>> From<T> for AuthId {
    fn from(identity: T) -> Self {
        AuthId::from_identity(identity)
    }
}

/// Non-anonymous client identity.
#[derive(Clone, Debug, Eq, Hash, PartialEq, Serialize, Deserialize)]
pub struct Identity(Arc<str>);

impl Identity {
    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl<T: AsRef<str>> From<T> for Identity {
    fn from(identity: T) -> Self {
        Self(identity.as_ref().into())
    }
}

impl Display for Identity {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        write!(f, "{}", self.as_str())
    }
}

impl<T: AsRef<str>> PartialEq<T> for Identity {
    fn eq(&self, other: &T) -> bool {
        self.as_str() == other.as_ref()
    }
}
