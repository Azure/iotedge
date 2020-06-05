use std::error::Error as StdError;

use async_trait::async_trait;

use mqtt_broker_core::auth::{AuthId, AuthenticationContext, Authenticator};

#[derive(Debug, Default)]
pub struct LocalAuthenticator;

impl LocalAuthenticator {
    pub fn new() -> Self {
        Self::default()
    }
}

#[async_trait]
impl Authenticator for LocalAuthenticator {
    type Error = Box<dyn StdError>;

    async fn authenticate(
        &self,
        context: AuthenticationContext,
    ) -> Result<Option<AuthId>, Self::Error> {
        let auth_id = if context.peer_addr().ip().is_loopback() {
            Some(AuthId::Identity(context.client_id().to_string()))
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
    use tokio::runtime::Runtime;

    use mqtt_broker_core::auth::{AuthId, AuthenticationContext, Authenticator};

    use super::LocalAuthenticator;

    #[test_case("127.0.0.1:12345"; "ipv4")]
    #[test_case("[::1]:12345"; "ipv6")]
    fn it_authenticates_client_id_when_localhost(peer_addr: &str) {
        let client_id = "client_1".into();
        let peer_addr = peer_addr.parse().unwrap();
        let context = AuthenticationContext::new(client_id, peer_addr);

        let authenticator = authenticator();
        let mut runtime = Runtime::new().expect("runtime");
        let auth_id = runtime.block_on(authenticator.authenticate(context));

        assert_matches!(auth_id, Ok(Some(AuthId::Identity(identity))) if identity == "client_1");
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
