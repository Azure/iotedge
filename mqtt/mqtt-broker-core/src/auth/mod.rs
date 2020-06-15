mod authentication;
mod authorization;

pub use authentication::{
    authenticate_fn_ok, AuthenticationContext, Authenticator, Certificate, DefaultAuthenticator,
};
pub use authorization::{
    authorize_fn_ok, Activity, Authorizer, Connect, DefaultAuthorizer, Operation, Publication,
    Publish, Subscribe, Authorization,
};

use std::fmt::{Display, Formatter, Result as FmtResult};

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
pub type Identity = String;
