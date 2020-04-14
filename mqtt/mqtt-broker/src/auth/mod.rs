mod authentication;
mod authorization;

pub use authentication::{
    AuthenticateError, Authenticator, Certificate, Credentials, DefaultAuthenticator,
};
pub use authorization::{Activity, AuthorizeError, Authorizer, DefaultAuthorizer, Operation};

/// Authenticated MQTT client identity.
#[derive(Clone, Debug, PartialEq)]
pub enum AuthId {
    /// Identity for anonymous client.
    Anonymous,

    /// Identity for non-anonymous client.
    Identity(Identity),
}

impl std::fmt::Display for AuthId {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
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
