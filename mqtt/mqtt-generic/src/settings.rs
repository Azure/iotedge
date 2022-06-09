use std::path::{Path, PathBuf};

use config::{Config, ConfigError, File, FileFormat};
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

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct Settings {
    listener: ListenerConfig,
    broker: BrokerConfig,
}

impl Settings {
    pub fn from_file<P: AsRef<Path>>(path: P) -> Result<Self, ConfigError> {
        let mut s = Config::new();
        s.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        s.merge(File::from(path.as_ref()))?;

        s.try_into()
    }

    pub fn broker(&self) -> &BrokerConfig {
        &self.broker
    }

    pub fn listener(&self) -> &ListenerConfig {
        &self.listener
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
}

impl ListenerConfig {
    pub fn new(tcp: Option<TcpTransportConfig>, tls: Option<TlsTransportConfig>) -> Self {
        Self {
            tcp: tcp.map(|tcp| Enable::from(Some(tcp))),
            tls: tls.map(|tls| Enable::from(Some(tls))),
        }
    }

    pub fn tcp(&self) -> Option<&TcpTransportConfig> {
        self.tcp.as_ref().and_then(Enable::as_inner)
    }

    pub fn tls(&self) -> Option<&TlsTransportConfig> {
        self.tls.as_ref().and_then(Enable::as_inner)
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
    certificate: CertificateConfig,
}

impl TlsTransportConfig {
    pub fn new(addr: impl Into<String>, certificate: CertificateConfig) -> Self {
        Self {
            addr: addr.into(),
            certificate,
        }
    }

    pub fn addr(&self) -> &str {
        &self.addr
    }

    pub fn certificate(&self) -> &CertificateConfig {
        &self.certificate
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

#[cfg(test)]
mod tests {
    use std::{path::Path, path::PathBuf, time::Duration};

    use matches::assert_matches;

    use mqtt_broker::settings::{
        BrokerConfig, HumanSize, QueueFullAction, RetainedMessagesConfig, SessionConfig,
        SessionPersistenceConfig,
    };

    use super::{ListenerConfig, Settings, TcpTransportConfig};

    const DAYS: u64 = 24 * 60 * 60;

    #[test]
    fn it_loads_defaults() {
        let settings = Settings::default();

        assert_eq!(
            settings,
            Settings {
                listener: ListenerConfig::new(Some(TcpTransportConfig::new("0.0.0.0:1883")), None,),
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

    #[test]
    fn it_overrides_defaults() {
        let settings = Settings::from_file(Path::new("test/config_correct.json"))
            .expect("should be able to create instance from configuration file");

        assert_eq!(
            settings.broker().retained_messages().expiration(),
            Duration::from_secs(90 * DAYS)
        );
    }

    #[test]
    fn it_type_mismatch_fails() {
        let settings = Settings::from_file(Path::new("test/config_bad_value_type.json"));

        assert_matches!(settings, Err(_));
    }

    #[test]
    fn it_overrides_messages_settings_with_zero() {
        let settings = Settings::from_file(Path::new("test/config_override_with_zero.json"))
            .expect("should be able to override settings with zero");

        assert_eq!(settings.broker().session().max_message_size(), None);
        assert_eq!(settings.broker().session().max_inflight_messages(), None);
        assert_eq!(settings.broker().session().max_queued_size(), None);
        assert_eq!(settings.broker().session().max_queued_messages(), None);
    }
}
