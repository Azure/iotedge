use config::{Config, ConfigError, Environment, File, FileFormat};
use serde::Deserialize;
use std::path::Path;
use std::vec::Vec;

pub const DEFAULTS: &str = include_str!("../config/default.json");
pub const ENVIRONMENT_PREFIX: &str = "iotedge";

#[derive(Debug, Clone, Default, PartialEq, Deserialize)]
pub struct Settings {
    #[serde(flatten)]
    nested_bridge: NestedBridgeSettings,

    #[serde(flatten)]
    subscriptions: Subscriptions,

    #[serde(flatten)]
    forwards: Forwards,
}

impl Settings {
    pub fn new() -> Result<Self, ConfigError> {
        let mut config = Config::new();

        config.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        config.merge(Environment::with_prefix(ENVIRONMENT_PREFIX))?;

        config.try_into()
    }

    pub fn from_file<P>(path: P) -> Result<Self, ConfigError>
    where
        P: AsRef<Path>,
    {
        let mut config = Config::new();

        config.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        config.merge(File::from(path.as_ref()))?;
        config.merge(Environment::with_prefix(ENVIRONMENT_PREFIX))?;

        config.try_into()
    }

    pub fn nested_bridge(&self) -> &NestedBridgeSettings {
        &self.nested_bridge
    }

    pub fn subscriptions(&self) -> &Subscriptions {
        &self.subscriptions
    }

    pub fn forwards(&self) -> &Forwards {
        &self.forwards()
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
    workload_uri: Option<String>,
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

#[derive(Debug, Clone, Default, PartialEq, Deserialize)]
pub struct Subscriptions {
    subscriptions: Vec<Subscription>,
}

impl Subscriptions {
    pub fn subscriptions(self) -> Vec<Subscription> {
        self.subscriptions
    }
}

#[derive(Debug, Clone, Default, PartialEq, Deserialize)]
pub struct Subscription {
    pattern: String,

    remote: String,
}

impl Subscription {
    pub fn pattern(&self) -> &str {
        &self.pattern
    }

    pub fn remote(&self) -> &str {
        &self.remote
    }
}

#[derive(Debug, Clone, Default, PartialEq, Deserialize)]
pub struct Forwards {
    forwards: Vec<Forward>,
}

impl Forwards {
    pub fn forwards(self) -> Vec<Forward> {
        self.forwards
    }
}

#[derive(Debug, Clone, Default, PartialEq, Deserialize)]
pub struct Forward {
    pattern: String,

    remote: String,
}

impl Forward {
    pub fn pattern(&self) -> &str {
        &self.pattern
    }

    pub fn remote(&self) -> &str {
        &self.remote
    }
}
