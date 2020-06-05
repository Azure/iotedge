use std::error::Error as StdError;

use async_trait::async_trait;

use mqtt_broker_core::auth::{AuthId, AuthenticationContext, Authenticator};

pub struct LocalAuthenticator;

impl LocalAuthenticator {
    pub fn new() -> Self {
        Self
    }
}

#[async_trait]
impl Authenticator for LocalAuthenticator {
    type Error = Box<dyn StdError>;

    async fn authenticate(
        &self,
        _context: AuthenticationContext,
    ) -> Result<Option<AuthId>, Self::Error> {
        todo!()
    }
}
