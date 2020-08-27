#![allow(dead_code)] // TODO remove when ready

use std::{path::Path, vec::Vec};

use config::{Config, ConfigError, Environment, File, FileFormat};
use serde::Deserialize;

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
        &self.forwards
    }
}

#[derive(Debug, Clone, Default, PartialEq, Deserialize)]
pub struct NestedBridgeSettings {
    #[serde(rename = "gatewayhostname")]
    gateway_hostname: Option<String>,

    #[serde(rename = "deviceid")]
    device_id: Option<String>,

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

    pub fn device_id(&self) -> Option<&str> {
        self.device_id.as_ref().map(AsRef::as_ref)
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

#[cfg(test)]
mod tests {
    use config::ConfigError;
    use serial_test::serial;

    use super::Settings;
    use mqtt_broker_tests_util::env;

    #[test]
    #[serial(env_settings)]
    fn new_overrides_settings_from_env() {
        it_overrides_settings_from_env(Settings::new);
    }

    #[test]
    #[serial(env_settings)]
    fn from_file_reads_nested_bridge_settings() {
        let settings = Settings::from_file("tests/config.json").unwrap();

        assert_eq!(
            settings.nested_bridge().gateway_hostname().unwrap(),
            "edge1"
        );
        assert_eq!(settings.nested_bridge().device_id().unwrap(), "d1");
        assert_eq!(settings.nested_bridge().module_id().unwrap(), "mymodule");
        assert_eq!(settings.nested_bridge().generation_id().unwrap(), "321");
        assert_eq!(settings.nested_bridge().workload_uri().unwrap(), "uri");
    }

    #[test]
    #[serial(env_settings)]
    fn from_file_overrides_settings_from_env() {
        it_overrides_settings_from_env(|| Settings::from_file("tests/config.json"));
    }

    fn it_overrides_settings_from_env<F>(make_settings: F)
    where
        F: FnOnce() -> Result<Settings, ConfigError>,
    {
        let _gateway_hostname = env::set_var("IOTEDGE_GATEWAYHOSTNAME", "upstream");
        let _device_id = env::set_var("IOTEDGE_DEVICEID", "device1");
        let _module_id = env::set_var("IOTEDGE_MODULEID", "m1");
        let _generation_id = env::set_var("IOTEDGE_MODULEGENERATIONID", "123");
        let _workload_uri = env::set_var("IOTEDGE_WORKLOADURI", "workload");

        let settings = make_settings().unwrap();

        assert_eq!(
            settings.nested_bridge().gateway_hostname().unwrap(),
            "upstream"
        );
        assert_eq!(settings.nested_bridge().device_id().unwrap(), "device1");
        assert_eq!(settings.nested_bridge().module_id().unwrap(), "m1");
        assert_eq!(settings.nested_bridge().generation_id().unwrap(), "123");
        assert_eq!(settings.nested_bridge().workload_uri().unwrap(), "workload");
    }
}
