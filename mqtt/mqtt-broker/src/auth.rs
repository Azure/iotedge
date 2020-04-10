use async_trait::async_trait;

/// Authenticated MQTT client identity.
#[derive(Clone, Debug, PartialEq)]
pub enum AuthId {
    /// Identity for anonymous client.
    Anonymous,

    /// Identity for non-anonymous client.
    Value(Identity),
}

impl std::fmt::Display for AuthId {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::Anonymous => write!(f, "*"),
            Self::Value(identity) => write!(f, "{}", identity),
        }
    }
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
#[derive(Clone, Debug)]
pub struct Certificate(Vec<u8>);

impl From<Vec<u8>> for Certificate {
    fn from(certificate: Vec<u8>) -> Self {
        Self(certificate)
    }
}

/// A trait to authenticate a MQTT client with given credentials.
#[async_trait]
pub trait Authenticator {
    /// Authentication error.
    type Error;

    /// Authenticates a MQTT client with given credentials.
    ///
    /// ## Returns
    /// * `Ok(Some(auth_id))` - authenticator is able to identify a client with given credentials.
    /// * `Ok(None)` - authenticator is not able to identify a client with given credentials.
    /// * `Err(e)` - an error occurred when authenticating a client.
    async fn authenticate(&self, credentials: Credentials) -> Result<Option<AuthId>, Self::Error>;
}

#[async_trait]
impl<F> Authenticator for F
where
    F: Fn(Credentials) -> Result<Option<AuthId>, AuthenticateError> + Sync,
{
    type Error = AuthenticateError;

    async fn authenticate(&self, credentials: Credentials) -> Result<Option<AuthId>, Self::Error> {
        self(credentials)
    }
}

/// Authentication error type placeholder.
#[derive(Debug, thiserror::Error)]
#[error("An error occurred authenticating client.")]
pub struct AuthenticateError;

/// Default implementation that always unable to authenticate a MQTT client and return `Ok(None)`.
/// This implementation will be used if custom authentication mechanism was not provided.
pub struct DefaultAuthenticator;

#[async_trait]
impl Authenticator for DefaultAuthenticator {
    type Error = AuthenticateError;

    async fn authenticate(&self, _: Credentials) -> Result<Option<AuthId>, Self::Error> {
        Ok(None)
    }
}

/// A trait to check a MQTT client permissions to perform some actions.
#[async_trait]
pub trait Authorizer {
    /// Authentication error.
    type Error;

    /// Authorizes a MQTT client to perform some action.
    async fn authorize(&self, auth_id: AuthId) -> Result<bool, Self::Error>;
}

#[async_trait]
impl<F> Authorizer for F
where
    F: Fn(AuthId) -> Result<bool, AuthorizeError> + Sync,
{
    type Error = AuthorizeError;

    async fn authorize(&self, auth_id: AuthId) -> Result<bool, Self::Error> {
        self(auth_id)
    }
}

/// Authorization error type placeholder.
#[derive(Debug, thiserror::Error)]
#[error("An error occurred checking client permissions.")]
pub struct AuthorizeError;

/// Default implementation that always denies any operation a client intends to perform.
/// This implementation will be used if custom authorization mechanism was not provided.
pub struct DefaultAuthorizer;

#[async_trait]
impl Authorizer for DefaultAuthorizer {
    type Error = AuthorizeError;

    async fn authorize(&self, _: AuthId) -> Result<bool, Self::Error> {
        Ok(false)
    }
}
