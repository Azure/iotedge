use std::error::Error as StdError;

use async_trait::async_trait;

use mqtt_broker_core::auth::{AuthId, Authenticator, Credentials};

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
        _username: Option<String>,
        _credentials: Credentials,
    ) -> Result<Option<AuthId>, Self::Error> {
        todo!()
    }
}
