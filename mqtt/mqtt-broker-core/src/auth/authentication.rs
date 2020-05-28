use std::convert::Infallible;

use async_trait::async_trait;

use crate::auth::AuthId;

/// Describes a MQTT client credentials.
pub enum Credentials {
    /// Password credentials.
    Password(Option<String>),

    /// Client certificate credentials.
    ClientCertificate(Certificate),
}

/// Represents a client certificate.
#[derive(Clone, Debug)]
pub struct Certificate(Vec<u8>);

impl AsRef<[u8]> for Certificate {
    fn as_ref(&self) -> &[u8] {
        self.0.as_ref()
    }
}

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
    async fn authenticate(
        &self,
        username: Option<String>,
        credentials: Credentials,
    ) -> Result<Option<AuthId>, Self::Error>;
}

#[async_trait]
impl<F, E> Authenticator for F
where
    F: Fn(Option<String>, Credentials) -> Result<Option<AuthId>, E> + Sync,
    E: std::error::Error + Send + 'static,
{
    type Error = E;

    async fn authenticate(
        &self,
        username: Option<String>,
        credentials: Credentials,
    ) -> Result<Option<AuthId>, Self::Error> {
        self(username, credentials)
    }
}

/// Default implementation that always unable to authenticate a MQTT client and return `Ok(None)`.
/// This implementation will be used if custom authentication mechanism was not provided.
pub struct DefaultAuthenticator;

#[async_trait]
impl Authenticator for DefaultAuthenticator {
    type Error = Infallible;

    async fn authenticate(
        &self,
        _: Option<String>,
        _: Credentials,
    ) -> Result<Option<AuthId>, Self::Error> {
        Ok(None)
    }
}

#[cfg(test)]
mod tests {
    use std::convert::Infallible;

    use matches::assert_matches;

    use crate::auth::{AuthId, Authenticator, Credentials, DefaultAuthenticator};

    #[tokio::test]
    async fn default_auth_always_return_unknown_client_identity() {
        let authenticator = DefaultAuthenticator;
        let credentials = Credentials::Password(Some("password".into()));

        let auth_id = authenticator.authenticate(Some("username".into()), credentials).await;

        assert_matches!(auth_id, Ok(None));
    }

    #[tokio::test]
    async fn authenticator_wrapper_around_function() {
        let authenticator = |_,_|  { Ok::<_, Infallible>(Some(AuthId::Anonymous))};
        let credentials = Credentials::Password(Some("password".into()));

        let auth_id = authenticator.authenticate(Some("username".into()), credentials).await;

        assert_matches!(auth_id, Ok(Some(AuthId::Anonymous)));
    }
}
