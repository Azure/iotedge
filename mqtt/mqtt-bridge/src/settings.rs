#![allow(dead_code)] // TODO remove when ready

use std::{path::Path, time::Duration, vec::Vec};

use config::{Config, ConfigError, Environment, File, FileFormat};
use serde::Deserialize;

pub const DEFAULTS: &str = include_str!("../config/default.json");
pub const ENVIRONMENT_PREFIX: &str = "iotedge";

#[derive(Debug, Clone, PartialEq)]
pub struct Settings {
    upstream: Option<ConnectionSettings>,

    remotes: Vec<ConnectionSettings>,

    messages: MessagesSettings,
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

    pub fn upstream(&self) -> Option<&ConnectionSettings> {
        self.upstream.as_ref()
    }

    pub fn remotes(&self) -> &Vec<ConnectionSettings> {
        &self.remotes
    }

    pub fn messages(&self) -> &MessagesSettings {
        &self.messages
    }
}

impl<'de> serde::Deserialize<'de> for Settings {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        #[derive(Debug, serde_derive::Deserialize)]
        struct Inner {
            #[serde(flatten)]
            nested_bridge: CredentialProviderSettings,

            upstream: UpstreamSettings,

            remotes: Vec<ConnectionSettings>,

            messages: MessagesSettings,
        }

        let value: Inner = serde::Deserialize::deserialize(deserializer)?;

        let upstream_connection_settings = match value.nested_bridge.gateway_hostname.clone() {
            Some(gateway_hostname) => Some(ConnectionSettings {
                name: String::from("upstream"),
                address: gateway_hostname,
                subscriptions: value.upstream.subscriptions,
                forwards: value.upstream.forwards,
                credentials: Credentials::Provider(value.nested_bridge),
                clean_session: value.upstream.clean_session,
                keep_alive: value.upstream.keep_alive,
            }),
            None => None,
        };

        Ok(Settings {
            upstream: upstream_connection_settings,
            remotes: value.remotes,
            messages: value.messages,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct ConnectionSettings {
    name: String,

    address: String,

    #[serde(flatten)]
    credentials: Credentials,

    subscriptions: Vec<Subscription>,

    forwards: Vec<Forward>,

    #[serde(with = "humantime_serde")]
    keep_alive: Duration,

    clean_session: bool,
}

impl ConnectionSettings {
    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn address(&self) -> &str {
        &self.address
    }

    pub fn credentials(&self) -> &Credentials {
        &self.credentials
    }

    pub fn subscriptions(&self) -> &Vec<Subscription> {
        &self.subscriptions
    }

    pub fn forwards(&self) -> &Vec<Forward> {
        &self.forwards
    }

    pub fn keep_alive(&self) -> &Duration {
        &self.keep_alive
    }

    pub fn clean_session(&self) -> bool {
        self.clean_session
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(untagged)]
pub enum Credentials {
    PlainText(AuthenticationSettings),
    Provider(CredentialProviderSettings),
}

#[derive(Debug, Clone, Default, PartialEq, Deserialize)]
pub struct AuthenticationSettings {
    client_id: String,

    username: String,

    password: String,
}

impl AuthenticationSettings {
    pub fn client_id(&self) -> &str {
        &self.client_id
    }

    pub fn username(&self) -> &str {
        &self.username
    }

    pub fn password(&self) -> &str {
        &self.password
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct CredentialProviderSettings {
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

impl CredentialProviderSettings {
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

#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct MessagesSettings {
    max_count: u32,
}

impl MessagesSettings {
    pub fn max_count(&self) -> u32 {
        self.max_count
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
struct UpstreamSettings {
    #[serde(with = "humantime_serde")]
    keep_alive: Duration,

    clean_session: bool,

    subscriptions: Vec<Subscription>,

    forwards: Vec<Forward>,
}

#[cfg(test)]
mod tests {
    use ::std::time::Duration;

    use config::ConfigError;
    use serial_test::serial;

    use super::Credentials;
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
        let upstream = settings.upstream().unwrap();

        assert_eq!(upstream.name(), "upstream");
        assert_eq!(upstream.address(), "edge1");

        match upstream.credentials() {
            Credentials::Provider(provider) => {
                assert_eq!(provider.device_id().unwrap(), "d1");
                assert_eq!(provider.module_id().unwrap(), "mymodule");
                assert_eq!(provider.generation_id().unwrap(), "321");
                assert_eq!(provider.workload_uri().unwrap(), "uri");
            }
            _ => panic!("Expected provider settings"),
        };
    }

    #[test]
    #[serial(env_settings)]
    fn from_file_reads_remotes_settings() {
        let settings = Settings::from_file("tests/config.json").unwrap();
        let len = settings.remotes().len();

        assert_eq!(len, 1);
        let remote = settings.remotes().first().unwrap();
        assert_eq!(remote.name(), "r1");
        assert_eq!(remote.address(), "remote");
        assert_eq!(remote.keep_alive().as_secs(), 60);
        assert_eq!(remote.clean_session(), false);

        match remote.credentials() {
            Credentials::PlainText(auth_settings) => {
                assert_eq!(auth_settings.username(), "mymodule");
                assert_eq!(auth_settings.password(), "pass");
                assert_eq!(auth_settings.client_id(), "client");
            }
            _ => panic!("Expected plaintext settings"),
        };
    }

    #[test]
    #[serial(env_settings)]
    fn from_file_reads_messages_settings() {
        let settings = Settings::from_file("tests/config.json").unwrap();

        assert_eq!(settings.messages.max_count(), 10);
    }

    #[test]
    #[serial(env_settings)]
    fn from_default_sets_messages_settings() {
        let settings = Settings::new().unwrap();

        assert_eq!(settings.messages.max_count(), 100);
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
        let upstream = settings.upstream().unwrap();

        assert_eq!(upstream.name(), "upstream");
        assert_eq!(upstream.address(), "upstream");

        match upstream.credentials() {
            Credentials::Provider(provider) => {
                assert_eq!(provider.device_id().unwrap(), "device1");
                assert_eq!(provider.module_id().unwrap(), "m1");
                assert_eq!(provider.generation_id().unwrap(), "123");
                assert_eq!(provider.workload_uri().unwrap(), "workload");
            }
            _ => panic!("Expected provider settings"),
        };

        env::remove_var("IOTEDGE_GATEWAYHOSTNAME");
        env::remove_var("IOTEDGE_DEVICEID");
        env::remove_var("IOTEDGE_MODULEID");
        env::remove_var("IOTEDGE_MODULEGENERATIONID");
        env::remove_var("IOTEDGE_WORKLOADURI");
    }
}
