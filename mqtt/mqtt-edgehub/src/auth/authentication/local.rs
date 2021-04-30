use std::convert::Infallible;

use async_trait::async_trait;

use mqtt_broker::{
    auth::{AuthenticationContext, Authenticator},
    AuthId,
};

/// Allows to connect any MQTT client connected to localhost.
/// It is intended to use to authenticate client for local communication
/// inside `EdgeHub` container.
#[derive(Debug, Default)]
pub struct LocalAuthenticator;

impl LocalAuthenticator {
    pub fn new() -> Self {
        Self::default()
    }
}

#[async_trait]
impl Authenticator for LocalAuthenticator {
    type Error = Infallible;

    async fn authenticate(
        &self,
        context: AuthenticationContext,
    ) -> Result<Option<AuthId>, Self::Error> {
        let auth_id = if context.peer_addr().ip().is_loopback() {
            Some(context.client_id().as_str().into())
        } else {
            None
        };

        Ok(auth_id)
    }
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;
    use test_case::test_case;

    use mqtt_broker::{
        auth::{AuthenticationContext, Authenticator, Identity},
        AuthId,
    };

    use super::LocalAuthenticator;

    #[test_case("127.0.0.1:12345"; "ipv4")]
    #[test_case("[::1]:12345"; "ipv6")]
    #[tokio::test]
    async fn it_authenticates_client_id_when_localhost(peer_addr: &str) {
        let client_id = "client_1".into();
        let peer_addr = peer_addr.parse().unwrap();
        let context = AuthenticationContext::new(client_id, peer_addr);

        let authenticator = authenticator();
        let auth_id = authenticator.authenticate(context).await;

        assert_matches!(auth_id, Ok(Some(AuthId::Identity(identity))) if identity == Identity::from("client_1"));
    }

    #[tokio::test]
    async fn it_blocks_client_from_no_localhost() {
        let client_id = "client_1".into();
        let peer_addr = "192.168.0.1:12345".parse().unwrap();
        let context = AuthenticationContext::new(client_id, peer_addr);

        let authenticator = authenticator();
        let auth_id = authenticator.authenticate(context).await;

        assert_matches!(auth_id, Ok(None));
    }

    fn authenticator() -> LocalAuthenticator {
        LocalAuthenticator::new()
    }
}
