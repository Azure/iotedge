use std::{
    collections::HashMap,
    path::{Path, PathBuf},
};

use config::{Config, ConfigError, Environment, File, FileFormat, Source, Value};
use lazy_static::lazy_static;
use serde::Deserialize;

use mqtt_bridge::BridgeSettings;
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

    // BrokerEnvironment allows only the env variables explicitly
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
    // and update `check_env_var_can_override_broker_settings` test.
    fn collect(&self) -> Result<HashMap<String, Value>, ConfigError> {
        let mut host_env = Config::new();
        // regular env vars
        host_env.merge(Environment::new())?;
        // broker specific vars
        host_env.merge(Environment::with_prefix("MqttBroker_").separator(":"))?;
        host_env.merge(Environment::with_prefix("MqttBroker_").separator("__"))?;

        let mut result: HashMap<String, config::Value> = HashMap::new();

        // session
        if let Ok(val) = host_env.get::<Value>("maxinflightmessages") {
            result.insert("broker.session.max_inflight_messages".into(), val);
        }
        if let Ok(val) = host_env.get::<Value>("maxqueuedmessages") {
            result.insert("broker.session.max_queued_messages".into(), val);
        }
        if let Ok(val) = host_env.get::<Value>("maxqueuedbytes") {
            result.insert("broker.session.max_queued_size".into(), val);
        }
        if let Ok(val) = host_env.get::<Value>("whenfull") {
            result.insert("broker.session.when_full".into(), val);
        }

        // persistance
        if let Ok(val) = host_env.get::<Value>("storagefolder") {
            result.insert("broker.persistence.folder_path".into(), val);
        }

        Ok(result)
    }
}

/// `BridgeEnvironment` is our custom implementation of `config::Source`
/// that can handle existing `EdgeHub` module env vars and convert them
/// into bridge upstream config structure and other settings.
#[derive(Debug, Clone)]
pub struct BridgeEnvironment;

impl Source for BridgeEnvironment {
    fn clone_into_box(&self) -> Box<dyn Source + Send + Sync> {
        Box::new((*self).clone())
    }

    // BridgeEnvironment allows only the env variables explicitly
    // defined in the method below.
    //
    // We use intermediate instance of `Config` to enumerate all env vars
    // and then manually map them to our internal config structure.
    // This is done for two reasons:
    // - our bridge config structure does not match legacy EdgeHub env vars,
    // - `Config` does a bunch of useful things - takes care of
    //   evn vars casing, prefixing, separators...
    //
    // NOTE: if adding new env vars - don't forget to use lowercase
    // and update `check_env_var_can_override_bridge_settings` test.
    fn collect(&self) -> Result<HashMap<String, Value>, ConfigError> {
        let mut host_env = Config::new();
        // regular env vars
        host_env.merge(Environment::new())?;
        // broker specific vars
        host_env.merge(Environment::with_prefix("MqttBridge_").separator(":"))?;
        host_env.merge(Environment::with_prefix("MqttBridge_").separator("__"))?;

        let mut result: HashMap<String, config::Value> = HashMap::new();

        // edgehub module upstream settings
        if let Ok(val) = host_env.get::<Value>("iotedge_iothubhostname") {
            result.insert("bridge.iothub_hostname".into(), val);
        }
        if let Ok(val) = host_env.get::<Value>("iotedge_gatewayhostname") {
            result.insert("bridge.gateway_hostname".into(), val);
        }
        if let Ok(val) = host_env.get::<Value>("iotedge_deviceid") {
            result.insert("bridge.device_id".into(), val);
        }
        if let Ok(val) = host_env.get::<Value>("iotedge_moduleid") {
            result.insert("bridge.module_id".into(), val);
        }
        if let Ok(val) = host_env.get::<Value>("iotedge_modulegenerationid") {
            result.insert("bridge.generation_id".into(), val);
        }
        if let Ok(val) = host_env.get::<Value>("iotedge_workloaduri") {
            result.insert("bridge.workload_uri".into(), val);
        }

        // storage ring buffer
        if let Ok(val) = host_env.get::<Value>("usepersistentstorage") {
            if val.to_string().to_lowercase() == "true" {
                result.insert("bridge.storage.type".into(), "ring_buffer".into());
            } else {
                result.insert("bridge.storage.type".into(), "memory".into());
            }
        }
        if let Ok(val) = host_env.get::<Value>("storagemaxfilesize") {
            result.insert("bridge.storage.max_file_size".into(), val);
        }
        if let Ok(val) = host_env.get::<Value>("storageflushoptions") {
            result.insert("bridge.storage.flush_options".into(), val);
        }
        if let Ok(val) = host_env.get::<Value>("storagefolder") {
            result.insert("bridge.storage.directory".into(), val);
        }

        // storage in memory
        if let Ok(val) = host_env.get::<Value>("storagemaxmessages") {
            result.insert("bridge.storage.max_size".into(), val);
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
    bridge: BridgeSettings,
}

impl Settings {
    pub fn new() -> Result<Self, ConfigError> {
        let mut config = Config::new();
        config.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        config.merge(BrokerEnvironment)?;
        config.merge(BridgeEnvironment)?;

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
        config.merge(BridgeEnvironment)?;

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

    pub fn bridge(&self) -> &BridgeSettings {
        &self.bridge
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
    use std::{
        num::{NonZeroU64, NonZeroUsize},
        path::PathBuf,
        time::Duration,
    };

    use serial_test::serial;

    use mqtt_bridge::{
        settings::{
            ConnectionSettings, Direction, MemorySettings, RingBufferSettings, StorageSettings,
            TopicRule,
        },
        BridgeSettings, FlushOptions,
    };
    use mqtt_broker::settings::{
        BrokerConfig, HumanSize, QueueFullAction, RetainedMessagesConfig, SessionConfig,
        SessionPersistenceConfig,
    };
    use mqtt_broker_tests_util::env;
    use mqtt_util::{AuthenticationSettings, CredentialProviderSettings, Credentials};

    use super::{AuthConfig, ListenerConfig, Settings, TcpTransportConfig, TlsTransportConfig};

    const DAYS: u64 = 24 * 60 * 60;
    const MINS: u64 = 60;

    #[test]
    #[serial(env_settings)]
    fn check_env_var_can_override_broker_settings() {
        let _max_inflight_messages = env::set_var("MqttBroker__MaxInflightMessages", "17");
        let _max_queued_messages = env::set_var("MqttBroker__MaxQueuedMessages", "1001");
        let _max_queued_bytes = env::set_var("MqttBroker__MaxQueuedBytes", "1");
        let _when_full = env::set_var("MqttBroker__WhenFull", "drop_old");

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
    }

    #[test]
    #[serial(env_settings)]
    fn check_env_var_can_override_persistence_settings() {
        let _storage_folder = env::set_var("StorageFolder", "/iotedge/storage");

        let settings = Settings::new().unwrap();

        assert_eq!(
            settings.broker().persistence(),
            &SessionPersistenceConfig::new(
                "/iotedge/storage".into(),
                Duration::from_secs(5 * MINS)
            )
        );
    }

    #[test]
    #[serial(env_settings)]
    fn check_env_var_can_override_bridge_settings() {
        // edgehub module upstream
        let _gateway_hostname = env::set_var("IOTEDGE_GATEWAYHOSTNAME", "edge1");
        let _device_id = env::set_var("IOTEDGE_DEVICEID", "device1");
        let _module_id = env::set_var("IOTEDGE_MODULEID", "m1");
        let _generation_id = env::set_var("IOTEDGE_MODULEGENERATIONID", "123");
        let _workload_uri = env::set_var("IOTEDGE_WORKLOADURI", "workload");
        let _iothub_hostname = env::set_var("IOTEDGE_IOTHUBHOSTNAME", "my_iothub");
        // storage
        let _storage_type = env::set_var("UsePersistentStorage", "true");
        let _storage_max_size = env::set_var("MqttBridge__StorageMaxFileSize", "256");
        let _storage_flush = env::set_var("MqttBridge__StorageFlushOptions", "off");
        let _storage_folder = env::set_var("StorageFolder", "/iotedge/storage");

        let settings = Settings::new().unwrap();
        assert_eq!(
            settings.bridge(),
            &BridgeSettings::new(
                Some(ConnectionSettings::new(
                    "$upstream",
                    "edge1:8883",
                    Credentials::Provider(CredentialProviderSettings::new(
                        "my_iothub",
                        "edge1",
                        "device1",
                        "m1",
                        "123",
                        "workload"
                    )),
                    Vec::new(),
                    Duration::from_secs(60),
                    false
                )),
                Vec::new(),
                StorageSettings::RingBuffer(RingBufferSettings::new(
                    NonZeroU64::new(256).expect("256"),
                    PathBuf::from("/iotedge/storage"),
                    FlushOptions::Off
                ))
            )
        );
    }

    #[test]
    #[serial(env_settings)]
    fn check_env_var_can_override_bridge_memory_storage_settings() {
        // edgehub module upstream
        let _gateway_hostname = env::set_var("IOTEDGE_GATEWAYHOSTNAME", "edge1");
        let _device_id = env::set_var("IOTEDGE_DEVICEID", "device1");
        let _module_id = env::set_var("IOTEDGE_MODULEID", "m1");
        let _generation_id = env::set_var("IOTEDGE_MODULEGENERATIONID", "123");
        let _workload_uri = env::set_var("IOTEDGE_WORKLOADURI", "workload");
        let _iothub_hostname = env::set_var("IOTEDGE_IOTHUBHOSTNAME", "my_iothub");
        // storage
        let _storage_type = env::set_var("UsePersistentStorage", "false");
        let _storage_max_size = env::set_var("MqttBridge__StorageMaxMessages", "256");

        let settings = Settings::new().unwrap();
        assert_eq!(
            settings.bridge().storage(),
            &StorageSettings::Memory(MemorySettings::new(NonZeroUsize::new(256).expect("256")))
        );
    }

    #[test]
    #[serial(env_settings)]
    fn it_loads_from_default_json() {
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
                ),
                bridge: BridgeSettings::new(
                    None,
                    Vec::new(),
                    StorageSettings::RingBuffer(RingBufferSettings::new(
                        NonZeroU64::new(33_554_432).expect("33554432"), //32mb
                        PathBuf::from("/tmp/mqttd/"),
                        FlushOptions::AfterEachWrite
                    ))
                )
            }
        );
    }

    #[test]
    #[serial(env_settings)]
    fn it_loads_from_file() {
        let settings = Settings::from_file("tests/settings/config.json").unwrap();

        assert_eq!(
            settings,
            Settings {
                listener: ListenerConfig::new(
                    Some(TcpTransportConfig::new("0.0.0.0:1883")),
                    Some(TlsTransportConfig::new("0.0.0.0:8883", None)),
                    TcpTransportConfig::new("0.0.0.0:1882"),
                ),
                auth: AuthConfig::new(7120, "/authenticate_file/"),
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
                        PathBuf::from("/tmp_file/mqttd/"),
                        Duration::from_secs(300)
                    )
                ),
                bridge: BridgeSettings::new(
                    None,
                    vec![ConnectionSettings::new(
                        "r1",
                        "remote:8883",
                        Credentials::PlainText(AuthenticationSettings::new(
                            "client", "mymodule", "pass", None
                        )),
                        vec![
                            Direction::In(TopicRule::new(
                                "temp/#",
                                None,
                                Some("floor/kitchen".into()),
                            )),
                            Direction::Out(TopicRule::new("some", None, Some("remote".into()),))
                        ],
                        Duration::from_secs(60),
                        false
                    )],
                    StorageSettings::RingBuffer(RingBufferSettings::new(
                        NonZeroU64::new(33_554_432).expect("33554432"), //32mb
                        PathBuf::from("/tmp_file/mqttd/"),
                        FlushOptions::Off
                    ))
                )
            }
        );
    }

    #[test]
    #[serial(env_settings)]
    fn it_verifies_broker_default_is_in_sync_with_default_json() {
        let settings = Settings::default();
        assert_eq!(settings.broker(), &BrokerConfig::default());
    }
}
