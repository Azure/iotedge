#![allow(dead_code)] // TODO remove when ready

use std::{path::Path, time::Duration, vec::Vec};

use config::{Config, ConfigError, Environment, File, FileFormat};
use serde::Deserialize;

use mqtt_util::client_io::{CredentialProviderSettings, Credentials};

pub const DEFAULTS: &str = include_str!("../config/default.json");
const DEFAULT_UPSTREAM_PORT: &str = "8883";

#[derive(Debug, Clone, PartialEq)]
pub struct BridgeSettings {
    upstream: Option<ConnectionSettings>,

    remotes: Vec<ConnectionSettings>,

    messages: MessagesSettings,
}

impl BridgeSettings {
    pub fn new() -> Result<Self, ConfigError> {
        let mut config = Config::new();

        config.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        config.merge(Environment::new())?;

        config.try_into()
    }

    pub fn from_file<P>(path: P) -> Result<Self, ConfigError>
    where
        P: AsRef<Path>,
    {
        let mut config = Config::new();

        config.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        config.merge(File::from(path.as_ref()))?;
        config.merge(Environment::new())?;

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

impl<'de> serde::Deserialize<'de> for BridgeSettings {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        #[derive(Debug, serde_derive::Deserialize)]
        struct Inner {
            #[serde(flatten)]
            nested_bridge: Option<CredentialProviderSettings>,

            upstream: UpstreamSettings,

            remotes: Vec<ConnectionSettings>,

            messages: MessagesSettings,
        }

        let Inner {
            nested_bridge,
            upstream,
            remotes,
            messages,
        } = serde::Deserialize::deserialize(deserializer)?;

        let upstream_connection_settings = nested_bridge.map(|nested_bridge| ConnectionSettings {
            name: "$upstream".into(),
            address: format!(
                "{}:{}",
                nested_bridge.gateway_hostname(),
                DEFAULT_UPSTREAM_PORT
            ),
            subscriptions: upstream.subscriptions,
            credentials: Credentials::Provider(nested_bridge),
            clean_session: upstream.clean_session,
            keep_alive: upstream.keep_alive,
        });

        Ok(BridgeSettings {
            upstream: upstream_connection_settings,
            remotes,
            messages,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct ConnectionSettings {
    name: String,

    address: String,

    #[serde(flatten)]
    credentials: Credentials,

    subscriptions: Vec<Direction>,

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

    pub fn subscriptions(&self) -> Vec<TopicRule> {
        self.subscriptions
            .iter()
            .filter_map(|sub| match sub {
                Direction::In(topic) | Direction::Both(topic) => Some(topic.clone()),
                _ => None,
            })
            .collect()
    }

    pub fn forwards(&self) -> Vec<TopicRule> {
        self.subscriptions
            .iter()
            .filter_map(|sub| match sub {
                Direction::Out(topic) | Direction::Both(topic) => Some(topic.clone()),
                _ => None,
            })
            .collect()
    }

    pub fn keep_alive(&self) -> Duration {
        self.keep_alive
    }

    pub fn clean_session(&self) -> bool {
        self.clean_session
    }
}

#[derive(Debug, Default, Clone, PartialEq, Deserialize)]
pub struct TopicRule {
    topic: String,

    #[serde(rename = "outPrefix")]
    out_prefix: Option<String>,

    #[serde(rename = "inPrefix")]
    in_prefix: Option<String>,
}

impl TopicRule {
    pub fn topic(&self) -> &str {
        &self.topic
    }

    pub fn out_prefix(&self) -> Option<&str> {
        self.out_prefix.as_deref()
    }

    pub fn in_prefix(&self) -> Option<&str> {
        self.in_prefix.as_deref()
    }

    pub fn subscribe_to(&self) -> String {
        match &self.in_prefix {
            Some(local) => {
                if local.is_empty() {
                    self.topic.clone()
                } else {
                    format!("{}/{}", local, self.topic)
                }
            }
            None => self.topic.clone(),
        }
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(tag = "direction")]
pub enum Direction {
    #[serde(rename = "in")]
    In(TopicRule),

    #[serde(rename = "out")]
    Out(TopicRule),

    #[serde(rename = "both")]
    Both(TopicRule),
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct MessagesSettings {}

#[derive(Debug, Clone, PartialEq, Deserialize)]
struct UpstreamSettings {
    #[serde(with = "humantime_serde")]
    keep_alive: Duration,

    clean_session: bool,

    subscriptions: Vec<Direction>,
}

#[cfg(test)]
mod tests {
    use config::ConfigError;
    use serial_test::serial;

    use super::BridgeSettings;
    use super::Credentials;
    use mqtt_broker_tests_util::env;

    #[test]
    #[serial(env_settings)]
    fn new_overrides_settings_from_env() {
        it_overrides_settings_from_env(BridgeSettings::new);
    }

    #[test]
    #[serial(env_settings)]
    fn new_no_upstream_settings() {
        let settings = BridgeSettings::new().unwrap();

        assert_eq!(settings.remotes().len(), 0);
        assert_eq!(settings.upstream(), None);
    }

    #[test]
    #[serial(env_settings)]
    fn from_file_reads_nested_bridge_settings() {
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let upstream = settings.upstream().unwrap();

        assert_eq!(upstream.name(), "$upstream");
        assert_eq!(upstream.address(), "edge1:8883");

        match upstream.credentials() {
            Credentials::Provider(provider) => {
                assert_eq!(provider.iothub_hostname(), "iothub");
                assert_eq!(provider.device_id(), "d1");
                assert_eq!(provider.module_id(), "mymodule");
                assert_eq!(provider.generation_id(), "321");
                assert_eq!(provider.workload_uri(), "uri");
            }
            _ => panic!("Expected provider settings"),
        };
    }

    #[test]
    #[serial(env_settings)]
    fn from_file_reads_remotes_settings() {
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();
        let len = settings.remotes().len();

        assert_eq!(len, 1);
        let remote = settings.remotes().first().unwrap();
        assert_eq!(remote.name(), "r1");
        assert_eq!(remote.address(), "remote:8883");
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
    fn from_default_sets_keepalive_settings() {
        let settings = BridgeSettings::from_file("tests/config.json").unwrap();

        assert_eq!(settings.upstream().unwrap().keep_alive().as_secs(), 60);
    }

    #[test]
    #[serial(env_settings)]
    fn from_file_overrides_settings_from_env() {
        it_overrides_settings_from_env(|| BridgeSettings::from_file("tests/config.json"));
    }

    #[test]
    #[serial(env_settings)]
    fn from_env_no_gateway_hostname() {
        let _device_id = env::set_var("IOTEDGE_DEVICEID", "device1");
        let _module_id = env::set_var("IOTEDGE_MODULEID", "m1");
        let _generation_id = env::set_var("IOTEDGE_MODULEGENERATIONID", "123");
        let _workload_uri = env::set_var("IOTEDGE_WORKLOADURI", "workload");
        let _iothub_hostname = env::set_var("IOTEDGE_IOTHUBHOSTNAME", "iothub");

        let settings = BridgeSettings::new().unwrap();

        assert_eq!(settings.upstream(), None);
    }

    fn it_overrides_settings_from_env<F>(make_settings: F)
    where
        F: FnOnce() -> Result<BridgeSettings, ConfigError>,
    {
        let _gateway_hostname = env::set_var("IOTEDGE_GATEWAYHOSTNAME", "upstream");
        let _device_id = env::set_var("IOTEDGE_DEVICEID", "device1");
        let _module_id = env::set_var("IOTEDGE_MODULEID", "m1");
        let _generation_id = env::set_var("IOTEDGE_MODULEGENERATIONID", "123");
        let _workload_uri = env::set_var("IOTEDGE_WORKLOADURI", "workload");
        let _iothub_hostname = env::set_var("IOTEDGE_IOTHUBHOSTNAME", "iothub");

        let settings = make_settings().unwrap();
        let upstream = settings.upstream().unwrap();

        assert_eq!(upstream.name(), "$upstream");
        assert_eq!(upstream.address(), "upstream:8883");

        match upstream.credentials() {
            Credentials::Provider(provider) => {
                assert_eq!(provider.iothub_hostname(), "iothub");
                assert_eq!(provider.device_id(), "device1");
                assert_eq!(provider.module_id(), "m1");
                assert_eq!(provider.generation_id(), "123");
                assert_eq!(provider.workload_uri(), "workload");
            }
            _ => panic!("Expected provider settings"),
        };
    }
}
