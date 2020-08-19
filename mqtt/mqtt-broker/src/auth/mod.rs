mod authentication;
mod authorization;

pub use authentication::{
    authenticate_fn_ok, AuthenticationContext, Authenticator, Certificate, DefaultAuthenticator,
};
pub use authorization::{
    authorize_fn_ok, Activity, AllowAll, Authorization, Authorizer, Connect, DenyAll, Operation,
    Publication, Publish, Subscribe,
};

use std::{
    fmt::{Display, Formatter, Result as FmtResult},
    sync::Arc,
};

use crate::ClientId;

/// Authenticated MQTT client identity.
#[derive(Clone, Debug, PartialEq)]
pub enum AuthId {
    /// Identity for anonymous client.
    Anonymous,

    /// Identity for non-anonymous client.
    Identity(Identity),
}

impl Display for AuthId {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        match self {
            Self::Anonymous => write!(f, "*"),
            Self::Identity(identity) => write!(f, "{}", identity),
        }
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
#[derive(Clone, Debug, Eq, Hash, PartialEq)]
pub struct Identity(Arc<String>);

impl Identity {
    fn as_str(&self) -> &str {
        &self.0
    }
}

impl<T: Into<String>> From<T> for Identity {
    fn from(s: T) -> Self {
        Self(Arc::new(s.into()))
    }
}

impl Display for Identity {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        write!(f, "{}", self.as_str())
    }
}

impl PartialEq<ClientId> for Identity {
    fn eq(&self, other: &ClientId) -> bool {
        self.as_str() == other.as_str()
    }
}
