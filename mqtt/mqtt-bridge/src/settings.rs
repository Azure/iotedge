use config::{Config, ConfigError, Environment};
use serde::Deserialize;

#[derive(Debug, Deserialize)]
pub struct Settings {
    #[serde(rename = "gatewayhostname")]
    gateway_hostname: Option<String>,
}

impl Settings {
    pub fn new() -> Result<Self, ConfigError> {
        let mut config = Config::new();
        config.merge(Environment::with_prefix("iotedge"))?;

        config.try_into()
    }

    pub fn gateway_hostname(&self) -> Option<&str> {
        self.gateway_hostname.as_ref().map(AsRef::as_ref)
    }
}
