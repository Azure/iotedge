use async_trait::async_trait;

use crate::auth::AuthId;

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
    type Error: std::error::Error + Send;

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

#[cfg(test)]
mod tests {
    use super::*;

    use matches::assert_matches;

    #[tokio::test]
    async fn default_auth_always_return_unknown_client_identity() {
        let authenticator = DefaultAuthenticator;
        let credentials = Credentials::Basic(Some("username".into()), Some("password".into()));

        let auth_id = authenticator.authenticate(credentials).await;

        assert_matches!(auth_id, Ok(None));
    }

    #[tokio::test]
    async fn authenticator_wrapper_around_function() {
        let authenticator = |_| Ok(Some(AuthId::Anonymous));
        let credentials = Credentials::Basic(Some("username".into()), Some("password".into()));

        let auth_id = authenticator.authenticate(credentials).await;

        assert_matches!(auth_id, Ok(Some(AuthId::Anonymous)));
    }
}
