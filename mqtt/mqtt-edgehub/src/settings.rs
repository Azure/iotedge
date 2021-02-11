use std::{
    collections::HashMap,
    path::{Path, PathBuf},
};

use config::{Config, ConfigError, Environment, File, FileFormat, Source, Value};
use lazy_static::lazy_static;
use serde::Deserialize;

use mqtt_broker::{settings::Enable, BrokerConfig};

pub const DEFAULTS: &str = include_str!("../config/default.json");

lazy_static! {
    static ref DEFAULT_CONFIG: Settings = {
        let mut s = Config::new();

        // It is guaranteed that next two calls must not fail,
        // otherwise we have a bug in the code or in ../config/default.json file.
        // It is guarded by a unit test as well.
        s.merge(File::from_str(DEFAULTS, FileFormat::Json)).expect(
            "Unable to load default broker config. Check default.json has invalid json format.",
        );
        s.try_into()
            .expect("Unable to load default broker config. Check default.json to match BrokerConfig structure.")
    };
}

/// `BrokerEnvironment` is our custom implementation of `config::Source`
/// that can handle existing `EdgeHub` env settings and convert them
/// into broker config structure.
#[derive(Debug, Clone)]
pub struct BrokerEnvironment;

impl Source for BrokerEnvironment {
    fn clone_into_box(&self) -> Box<dyn Source + Send + Sync> {
        Box::new((*self).clone())
    }

    // Currently, BrokerEnvironment allows only the env variables explicitly
    // defined in the method below.
    //
    // We use intermediate instance of `Config` to enumerate all env vars
    // and then manually map them to our internal config structure.
    // This is done for two reasons:
    // - our broker config structure does not match legacy EdgeHub env vars,
    // - `Config` does a bunch of useful things - takes care of
    //   evn vars casing, prefixing, separators...
    //
    // NOTE: if adding new env vars - don't forget to use lowercase
    // and update `check_env_var_name_override` test.
    fn collect(&self) -> Result<HashMap<String, Value>, ConfigError> {
        let mut host_env = Config::new();
        // regular env vars
        host_env.merge(Environment::new())?;
        // broker specific vars
        host_env.merge(Environment::with_prefix("MqttBroker_").separator(":"))?;
        host_env.merge(Environment::with_prefix("MqttBroker_").separator("__"))?;

        let mut result: HashMap<String, config::Value> = HashMap::new();

        // session
        if let Ok(val) = host_env.get::<String>("maxinflightmessages") {
            result.insert("broker.session.max_inflight_messages".into(), val.into());
        }
        if let Ok(val) = host_env.get::<String>("maxqueuedmessages") {
            result.insert("broker.session.max_queued_messages".into(), val.into());
        }
        if let Ok(val) = host_env.get::<String>("maxqueuedbytes") {
            result.insert("broker.session.max_queued_size".into(), val.into());
        }
        if let Ok(val) = host_env.get::<String>("whenfull") {
            result.insert("broker.session.when_full".into(), val.into());
        }

        // persistance
        if let Ok(val) = host_env.get::<String>("storagefolder") {
            result.insert("broker.persistence.folder_path".into(), val.clone().into());
            result.insert("bridge.persistence.folder_path".into(), val.into());
        }

        Ok(result)
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct Settings {
    listener: ListenerConfig,
    broker: BrokerConfig,
    auth: AuthConfig,
}

impl Settings {
    pub fn new() -> Result<Self, ConfigError> {
        let mut config = Config::new();
        config.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        config.merge(BrokerEnvironment)?;

        config.try_into()
    }

    pub fn from_file<P>(path: P) -> Result<Self, ConfigError>
    where
        P: AsRef<Path>,
    {
        let mut config = Config::new();
        config.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        config.merge(File::from(path.as_ref()))?;
        config.merge(BrokerEnvironment)?;

        config.try_into()
    }

    pub fn broker(&self) -> &BrokerConfig {
        &self.broker
    }

    pub fn listener(&self) -> &ListenerConfig {
        &self.listener
    }

    pub fn auth(&self) -> &AuthConfig {
        &self.auth
    }
}

impl Default for Settings {
    fn default() -> Self {
        DEFAULT_CONFIG.clone()
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct ListenerConfig {
    tcp: Option<Enable<TcpTransportConfig>>,
    tls: Option<Enable<TlsTransportConfig>>,
    system: TcpTransportConfig,
}

impl ListenerConfig {
    pub fn new(
        tcp: Option<TcpTransportConfig>,
        tls: Option<TlsTransportConfig>,
        system: TcpTransportConfig,
    ) -> Self {
        Self {
            tcp: tcp.map(|tcp| Enable::from(Some(tcp))),
            tls: tls.map(|tls| Enable::from(Some(tls))),
            system,
        }
    }

    pub fn tcp(&self) -> Option<&TcpTransportConfig> {
        self.tcp.as_ref().and_then(Enable::as_inner)
    }

    pub fn tls(&self) -> Option<&TlsTransportConfig> {
        self.tls.as_ref().and_then(Enable::as_inner)
    }

    pub fn system(&self) -> &TcpTransportConfig {
        &self.system
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct TcpTransportConfig {
    #[serde(rename = "address")]
    addr: String,
}

impl TcpTransportConfig {
    pub fn new(addr: impl Into<String>) -> Self {
        Self { addr: addr.into() }
    }

    pub fn addr(&self) -> &str {
        &self.addr
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct TlsTransportConfig {
    #[serde(rename = "address")]
    addr: String,

    #[serde(flatten)]
    certificate: Option<CertificateConfig>,
}

impl TlsTransportConfig {
    pub fn new(addr: impl Into<String>, certificate: Option<CertificateConfig>) -> Self {
        Self {
            addr: addr.into(),
            certificate,
        }
    }

    pub fn addr(&self) -> &str {
        &self.addr
    }

    pub fn certificate(&self) -> Option<&CertificateConfig> {
        self.certificate.as_ref()
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct CertificateConfig {
    #[serde(rename = "certificate")]
    cert_path: PathBuf,

    #[serde(rename = "private_key")]
    private_key_path: PathBuf,
}

impl CertificateConfig {
    pub fn new(cert_path: impl Into<PathBuf>, private_key_path: impl Into<PathBuf>) -> Self {
        Self {
            cert_path: cert_path.into(),
            private_key_path: private_key_path.into(),
        }
    }

    pub fn cert_path(&self) -> &Path {
        &self.cert_path
    }

    pub fn private_key_path(&self) -> &Path {
        &self.private_key_path
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct AuthConfig {
    port: u16,
    base_url: String,
}

impl AuthConfig {
    pub fn new(port: u16, base_url: impl Into<String>) -> Self {
        Self {
            port,
            base_url: base_url.into(),
        }
    }

    pub fn url(&self) -> String {
        format!("http://localhost:{}{}", self.port, self.base_url)
    }
}

#[cfg(test)]
mod tests {
    use std::{path::PathBuf, time::Duration};

    use serial_test::serial;

    use mqtt_broker::settings::{
        BrokerConfig, HumanSize, QueueFullAction, RetainedMessagesConfig, SessionConfig,
        SessionPersistenceConfig,
    };
    use mqtt_broker_tests_util::env;

    use super::{AuthConfig, ListenerConfig, Settings, TcpTransportConfig, TlsTransportConfig};

    const DAYS: u64 = 24 * 60 * 60;
    const MINS: u64 = 60;

    #[test]
    #[serial(env_settings)]
    fn check_env_var_name_override() {
        let _max_inflight_messages = env::set_var("MqttBroker__MaxInflightMessages", "17");
        let _max_queued_messages = env::set_var("MqttBroker__MaxQueuedMessages", "1001");
        let _max_queued_bytes = env::set_var("MqttBroker__MaxQueuedBytes", "1");
        let _when_full = env::set_var("MqttBroker__WhenFull", "drop_old");
        let _storage_folder = env::set_var("StorageFolder", "/iotedge/storage");

        let settings = Settings::new().unwrap();

        assert_eq!(
            settings.broker().session(),
            &SessionConfig::new(
                Duration::from_secs(60 * DAYS),
                Duration::from_secs(DAYS), // 1d
                Some(HumanSize::new_kilobytes(256).expect("256kb")),
                17,
                1001,
                Some(HumanSize::new_bytes(1)),
                QueueFullAction::DropOld,
            )
        );
        assert_eq!(
            settings.broker().persistence(),
            &SessionPersistenceConfig::new(
                "/iotedge/storage".into(),
                Duration::from_secs(5 * MINS)
            )
        );
    }

    #[test]
    fn it_loads_defaults() {
        let settings = Settings::default();

        assert_eq!(
            settings,
            Settings {
                listener: ListenerConfig::new(
                    Some(TcpTransportConfig::new("0.0.0.0:1883")),
                    Some(TlsTransportConfig::new("0.0.0.0:8883", None)),
                    TcpTransportConfig::new("0.0.0.0:1882"),
                ),
                auth: AuthConfig::new(7120, "/authenticate/"),
                broker: BrokerConfig::new(
                    RetainedMessagesConfig::new(1000, Duration::from_secs(60 * DAYS)),
                    SessionConfig::new(
                        Duration::from_secs(60 * DAYS),
                        Duration::from_secs(DAYS), // 1d
                        Some(HumanSize::new_kilobytes(256).expect("256kb")),
                        16,
                        1000,
                        Some(HumanSize::new_bytes(0)),
                        QueueFullAction::DropNew,
                    ),
                    SessionPersistenceConfig::new(
                        PathBuf::from("/tmp/mqttd/"),
                        Duration::from_secs(300)
                    )
                )
            }
        );
    }

    #[test]
    fn it_verifies_broker_config_defaults() {
        let settings = Settings::default();
        assert_eq!(settings.broker(), &BrokerConfig::default());
    }
}
