use config::{Config, ConfigError, Environment};
use serde::Deserialize;

#[derive(Debug, Clone, Default, PartialEq, Deserialize)]
pub struct Settings {
    #[serde(flatten)]
    nested_bridge: NestedBridgeSettings,
}

impl Settings {
    pub fn new() -> Result<Self, ConfigError> {
        let mut config = Config::new();
        config.merge(Environment::with_prefix("iotedge"))?;

        config.try_into()
    }

    pub fn nested_bridge(&self) -> &NestedBridgeSettings {
        &self.nested_bridge
    }
}

#[derive(Debug, Clone, Default, PartialEq, Deserialize)]
pub struct NestedBridgeSettings {
    #[serde(rename = "gatewayhostname")]
    gateway_hostname: Option<String>,

    #[serde(rename = "moduleid")]
    module_id: Option<String>,

    #[serde(rename = "modulegenerationid")]
    generation_id: Option<String>,

    #[serde(rename = "workloaduri")]
    workload_uri: Option<String>
}

impl NestedBridgeSettings {
    pub fn gateway_hostname(&self) -> Option<&str> {
        self.gateway_hostname.as_ref().map(AsRef::as_ref)
    }

    pub fn module_id(&self) -> Option<&str> {
        self.module_id.as_ref().map(AsRef::as_ref)
    }

    pub fn generation_id(&self) -> Option<&str> {
        self.generation_id.as_ref().map(AsRef::as_ref)
    }

    pub fn workload_uri(&self) -> Option<&str> {
        self.workload_uri.as_ref().map(AsRef::as_ref)
    }
}
