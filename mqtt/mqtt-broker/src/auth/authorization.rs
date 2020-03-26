use async_trait::async_trait;

use crate::{AuthId, Error};

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

#[cfg(test)]
mod tests {
    use super::*;

    use matches::assert_matches;

    #[tokio::test]
    async fn default_auth_always_deny_any_action() {
        let auth = DefaultAuthorizer;
        let auth_id = AuthId::Value("client-a".into());

        let res = auth.authorize(auth_id).await;

        assert_matches!(res, Ok(false));
    }

    #[tokio::test]
    async fn authorizer_wrapper_around_function() {
        let auth = |_| Ok(true);
        let auth_id = AuthId::Value("client-a".into());

        let res = auth.authorize(auth_id).await;

        assert_matches!(res, Ok(true));
    }
}
