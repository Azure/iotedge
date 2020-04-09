use derive_more::Display;
use failure::Fail;

mod authentication;
mod authorization;

pub use authentication::{Authenticator, Certificate, Credentials, DefaultAuthenticator};
pub use authorization::{Activity, Authorizer, DefaultAuthorizer, Operation};

/// Authenticated MQTT client identity.
#[derive(Clone, Debug, Display, PartialEq)]
pub enum AuthId {
    /// Identity for anonymous client.
    #[display(fmt = "*")]
    Anonymous,

    /// Identity for identified client.
    Identity(Identity),
}

impl AuthId {
    /// Creates a MQTT identity for known client.
    pub fn from_identity<T: Into<Identity>>(identity: T) -> Self {
        Self::Identity(identity.into())
    }

    /// Creates an anonymous MQTT client identity.
    pub fn anonymous() -> Self {
        AuthId::Anonymous
    }
}

impl<T: Into<Identity>> From<T> for AuthId {
    fn from(identity: T) -> Self {
        AuthId::from_identity(identity)
    }
}

/// Non-anonymous client identity.
pub type Identity = String;

/// Represents reason for failed auth operations.
#[derive(Debug, Display, Fail, PartialEq)]
pub enum ErrorReason {
    #[display(fmt = "Error occurred during authentication")]
    Authenticate,

    #[display(fmt = "Error occurred during authorization")]
    Authorize,
}
