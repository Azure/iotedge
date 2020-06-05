use std::{error::Error as StdError, net::SocketAddr, ops::Deref};

use async_trait::async_trait;

use crate::auth::AuthId;

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
    type Error: Deref<Target = dyn StdError>;

    /// Authenticates a MQTT client with given credentials.
    ///
    /// ## Returns
    /// * `Ok(Some(auth_id))` - authenticator is able to identify a client with given credentials.
    /// * `Ok(None)` - authenticator is not able to identify a client with given credentials.
    /// * `Err(e)` - an error occurred when authenticating a client.
    async fn authenticate(
        &self,
        context: AuthenticationContext,
    ) -> Result<Option<AuthId>, Self::Error>;
}

/// A data required to authenticate connected client.
#[derive(Debug)]
pub struct AuthenticationContext {
    username: Option<String>,
    password: Option<String>,
    certificate: Option<Certificate>,
    peer_addr: SocketAddr,
}

impl AuthenticationContext {
    pub fn new(peer_addr: SocketAddr) -> Self {
        Self {
            username: None,
            password: None,
            certificate: None,
            peer_addr,
        }
    }

    pub fn with_username(&mut self, username: impl Into<String>) -> &mut Self {
        self.username = Some(username.into());
        self
    }

    pub fn with_password(&mut self, password: impl Into<String>) -> &mut Self {
        self.password = Some(password.into());
        self
    }

    pub fn with_certificate(&mut self, certificate: impl Into<Certificate>) -> &mut Self {
        self.certificate = Some(certificate.into());
        self
    }

    pub fn username(&self) -> Option<&str> {
        self.username.as_deref()
    }

    pub fn password(&self) -> Option<&str> {
        self.password.as_deref()
    }

    pub fn certificate(&self) -> Option<&Certificate> {
        self.certificate.as_ref()
    }

    pub fn peer_addr(&self) -> SocketAddr {
        self.peer_addr
    }
}

/// Creates an authenticator from a function.
/// It wraps any provided function with an interface aligned with authenticator.
pub fn authenticate_fn_ok<F>(f: F) -> impl Authenticator<Error = Box<dyn StdError>>
where
    F: Fn(AuthenticationContext) -> Option<AuthId> + Sync + 'static,
{
    move |context| Ok(f(context))
}

#[async_trait]
impl<F, E> Authenticator for F
where
    F: Fn(AuthenticationContext) -> Result<Option<AuthId>, E> + Sync,
    E: Deref<Target = dyn StdError> + 'static,
{
    type Error = E;

    async fn authenticate(
        &self,
        context: AuthenticationContext,
    ) -> Result<Option<AuthId>, Self::Error> {
        self(context)
    }
}

/// Default implementation that always unable to authenticate a MQTT client and return `Ok(None)`.
/// This implementation will be used if custom authentication mechanism was not provided.
pub struct DefaultAuthenticator;

#[async_trait]
impl Authenticator for DefaultAuthenticator {
    type Error = Box<dyn StdError>;

    async fn authenticate(&self, _: AuthenticationContext) -> Result<Option<AuthId>, Self::Error> {
        Ok(None)
    }
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;

    use crate::auth::{
        authenticate_fn_ok, AuthId, AuthenticationContext, Authenticator, DefaultAuthenticator,
    };

    #[tokio::test]
    async fn default_auth_always_return_unknown_client_identity() {
        let authenticator = DefaultAuthenticator;
        let context = AuthenticationContext::new("127.0.0.1:12345".parse().unwrap());

        let auth_id = authenticator.authenticate(context).await;

        assert_matches!(auth_id, Ok(None));
    }

    #[tokio::test]
    async fn authenticator_wrapper_around_function() {
        let authenticator = authenticate_fn_ok(|context| {
            if context.username() == Some("username") {
                Some(AuthId::Identity("username".to_string()))
            } else {
                None
            }
        });

        let mut context = AuthenticationContext::new("127.0.0.1:12345".parse().unwrap());
        context.with_username("username");

        let auth_id = authenticator.authenticate(context).await;

        assert_matches!(auth_id, Ok(Some(AuthId::Identity(identity))) if identity == "username");
    }
}
