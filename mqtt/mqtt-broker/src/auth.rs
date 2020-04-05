use async_trait::async_trait;
use derive_more::{Display, From};
use failure::Fail;

use crate::Error;

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

/// Describes a MQTT client credentials.
pub enum Credentials {
    /// Basic username and password credentials.
    Basic(Option<String>, Option<String>),

    /// Client certificate credentials.
    ClientCertificate(Certificate),
}

/// Represents a client certificate.
#[derive(Clone, Debug, From)]
pub struct Certificate(Vec<u8>);

/// A trait to authenticate a MQTT client with given credentials.
#[async_trait]
pub trait Authenticator {
    /// Authenticates a MQTT client with given credentials.
    ///
    /// ## Returns
    /// * `Ok(Some(auth_id))` - authenticator is able to identify a client with given credentials.
    /// * `Ok(None)` - authenticator is not able to identify a client with given credentials.
    /// * `Err(e)` - an error occurred when authenticating a client.
    async fn authenticate(&self, credentials: Credentials) -> Result<Option<AuthId>, Error>;
}

#[async_trait]
impl<F> Authenticator for F
where
    F: Fn(Credentials) -> Result<Option<AuthId>, Error> + Sync,
{
    async fn authenticate(&self, credentials: Credentials) -> Result<Option<AuthId>, Error> {
        self(credentials)
    }
}

/// Default implementation that always unable to authenticate a MQTT client and return `Ok(None)`.
/// This implementation will be used if custom authentication mechanism was not provided.
pub struct DefaultAuthenticator;

#[async_trait]
impl Authenticator for DefaultAuthenticator {
    async fn authenticate(&self, _: Credentials) -> Result<Option<AuthId>, Error> {
        Ok(None)
    }
}

/// A trait to check a MQTT client permissions to perform some actions.
#[async_trait]
pub trait Authorizer {
    /// Authorizes a MQTT client to perform some action.
    async fn authorize(&self, auth_id: AuthId) -> Result<bool, Error>;
}

#[async_trait]
impl<F> Authorizer for F
where
    F: Fn(AuthId) -> Result<bool, Error> + Sync,
{
    async fn authorize(&self, auth_id: AuthId) -> Result<bool, Error> {
        self(auth_id)
    }
}

/// Default implementation that always denies any operation a client intends to perform.
/// This implementation will be used if custom authorization mechanism was not provided.
pub struct DefaultAuthorizer;

#[async_trait]
impl Authorizer for DefaultAuthorizer {
    async fn authorize(&self, _: AuthId) -> Result<bool, Error> {
        Ok(false)
    }
}

/// Represents reason for failed auth operations.
#[derive(Debug, Display, Fail, PartialEq)]
pub enum ErrorReason {
    #[display(fmt = "Error occurred during authentication")]
    Authenticate,

    #[display(fmt = "Error occurred during authorization")]
    Authorize,
}
