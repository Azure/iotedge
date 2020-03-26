use derive_more::Display;
use failure::Fail;

mod authentication;
mod authorization;

pub use authentication::{Authenticator, Certificate, Credentials, DefaultAuthenticator};
pub use authorization::{Authorizer, DefaultAuthorizer};

/// Authenticated MQTT client identity.
#[derive(Clone, Debug, Display, PartialEq)]
pub enum AuthId {
    /// Identity for anonymous client.
    #[display(fmt = "*")]
    Anonymous,

    /// Identity for non-anonymous client.
    Value(Identity),
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
