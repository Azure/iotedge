use std::{convert::Infallible, error::Error as StdError, fmt::Display, net::SocketAddr};

use async_trait::async_trait;

use crate::{auth::AuthId, ClientId};

/// Represents a client certificate.
#[derive(Clone, Debug)]
pub struct Certificate(String);

impl AsRef<[u8]> for Certificate {
    fn as_ref(&self) -> &[u8] {
        self.0.as_ref()
    }
}

impl AsRef<str> for Certificate {
    fn as_ref(&self) -> &str {
        self.0.as_ref()
    }
}

impl From<String> for Certificate {
    fn from(certificate: String) -> Self {
        Self(certificate)
    }
}

/// A trait to authenticate a MQTT client with given credentials.
#[async_trait]
pub trait Authenticator {
    /// Authentication error.
    type Error: Display;

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
    client_id: ClientId,
    peer_addr: SocketAddr,
    username: Option<String>,
    password: Option<String>,
    certificate: Option<Certificate>,
    cert_chain: Option<Vec<Certificate>>,
}

impl AuthenticationContext {
    pub fn new(client_id: ClientId, peer_addr: SocketAddr) -> Self {
        Self {
            client_id,
            peer_addr,
            username: None,
            password: None,
            certificate: None,
            cert_chain: None,
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

    pub fn with_cert_chain(&mut self, chain: Vec<Certificate>) -> &mut Self {
        self.cert_chain = Some(chain);
        self
    }

    pub fn client_id(&self) -> &ClientId {
        &self.client_id
    }

    pub fn peer_addr(&self) -> SocketAddr {
        self.peer_addr
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

    pub fn cert_chain(&self) -> Option<&Vec<Certificate>> {
        self.cert_chain.as_ref()
    }
}

/// Creates an authenticator from a function.
/// It wraps any provided function with an interface aligned with authenticator.
pub fn authenticate_fn_ok<F>(f: F) -> impl Authenticator<Error = Infallible>
where
    F: Fn(AuthenticationContext) -> Option<AuthId> + Sync + 'static,
{
    move |context| Ok(f(context))
}

#[async_trait]
impl<F, E> Authenticator for F
where
    F: Fn(AuthenticationContext) -> Result<Option<AuthId>, E> + Sync,
    E: StdError + 'static,
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
    type Error = Infallible;

    async fn authenticate(&self, _: AuthenticationContext) -> Result<Option<AuthId>, Self::Error> {
        Ok(None)
    }
}

/// Wrapper to take any authenticator and wrap error type as a trait object.
pub struct DynAuthenticator<N> {
    inner: N,
}

#[async_trait]
impl<N, E> Authenticator for DynAuthenticator<N>
where
    N: Authenticator<Error = E> + Send + Sync,
    E: StdError + Into<Box<dyn StdError>> + Send + Sync + 'static,
{
    type Error = Box<dyn StdError + Send + Sync>;

    async fn authenticate(
        &self,
        context: AuthenticationContext,
    ) -> Result<Option<AuthId>, Self::Error> {
        let auth_id = self.inner.authenticate(context).await?;
        Ok(auth_id)
    }
}

impl<C> From<C> for DynAuthenticator<C> {
    fn from(command: C) -> Self {
        Self { inner: command }
    }
}

#[cfg(test)]
mod tests {
    use std::net::SocketAddr;

    use matches::assert_matches;

    use super::{
        authenticate_fn_ok, AuthId, AuthenticationContext, Authenticator, DefaultAuthenticator,
    };
    use crate::auth::Identity;

    #[tokio::test]
    async fn default_auth_always_return_unknown_client_identity() {
        let authenticator = DefaultAuthenticator;
        let context = AuthenticationContext::new("client_1".into(), peer_addr());

        let auth_id = authenticator.authenticate(context).await;

        assert_matches!(auth_id, Ok(None));
    }

    #[tokio::test]
    async fn authenticator_wrapper_around_function() {
        let authenticator = authenticate_fn_ok(|context| {
            if context.username() == Some("username") {
                Some("username".into())
            } else {
                None
            }
        });

        let mut context = AuthenticationContext::new("client_1".into(), peer_addr());
        context.with_username("username");

        let auth_id = authenticator.authenticate(context).await;

        assert_matches!(auth_id, Ok(Some(AuthId::Identity(identity))) if identity == Identity::from("username"));
    }

    fn peer_addr() -> SocketAddr {
        "127.0.0.1:12345".parse().unwrap()
    }
}
