use std::path::{Path, PathBuf};

use config::{Config, ConfigError, Environment, File, FileFormat};
use lazy_static::lazy_static;
use serde::Deserialize;

use mqtt_broker_core::settings::BrokerConfig;

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
    auth: AuthConfig,
}

impl Settings {
    pub fn new() -> Result<Self, ConfigError> {
        let mut config = Config::new();
        config.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        config.merge(Environment::new().separator("__"))?;

        config.try_into()
    }

    pub fn from_file<P>(path: P) -> Result<Self, ConfigError>
    where
        P: AsRef<Path>,
    {
        let mut config = Config::new();
        config.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        config.merge(File::from(path.as_ref()))?;
        config.merge(Environment::new().separator("__"))?;

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
    tcp: Option<TcpTransportConfig>,
    tls: Option<TlsTransportConfig>,
    system: TcpTransportConfig,
}

impl ListenerConfig {
    pub fn tcp(&self) -> Option<&TcpTransportConfig> {
        self.tcp.as_ref()
    }

    pub fn tls(&self) -> Option<&TlsTransportConfig> {
        self.tls.as_ref()
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

    #[serde(rename = "certificate")]
    cert_path: Option<PathBuf>,
}

impl TlsTransportConfig {
    pub fn new(addr: impl Into<String>, cert_path: Option<impl Into<PathBuf>>) -> Self {
        Self {
            addr: addr.into(),
            cert_path: cert_path.map(Into::into),
        }
    }

    pub fn addr(&self) -> &str {
        &self.addr
    }

    pub fn cert_path(&self) -> Option<&Path> {
        self.cert_path.as_deref()
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
    use std::{env, time::Duration};

    use mqtt_broker_core::settings::{
        BrokerConfig, HumanSize, QueueFullAction, RetainedMessagesConfig, SessionConfig,
    };

    use super::{AuthConfig, ListenerConfig, Settings, TcpTransportConfig, TlsTransportConfig};
    use config::ConfigError;

    const DAYS: u64 = 24 * 60 * 60;

    #[test]
    fn it_loads_defaults() {
        let settings = Settings::default();

        assert_eq!(
            settings,
            Settings {
                listener: ListenerConfig {
                    tcp: Some(TcpTransportConfig::new("0.0.0.0:1883")),
                    tls: Some(TlsTransportConfig::new(
                        "0.0.0.0:8883",
                        Option::<&str>::None
                    )),
                    system: TcpTransportConfig::new("0.0.0.0:1882"),
                },
                auth: AuthConfig::new(7120, "/authenticate/"),
                broker: BrokerConfig::new(
                    RetainedMessagesConfig::new(1000, Duration::from_secs(60 * DAYS)),
                    SessionConfig::new(
                        Duration::from_secs(60 * DAYS),
                        Some(HumanSize::new_kilobytes(256).expect("256kb")),
                        16,
                        1000,
                        Some(HumanSize::new_bytes(0)),
                        QueueFullAction::DropNew,
                    ),
                    None
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
    fn new_overrides_settings_from_env() {
        it_overrides_settings_from_env(Settings::new);
    }

    #[test]
    fn from_file_overrides_settings_from_env() {
        it_overrides_settings_from_env(|| Settings::from_file("config/default.json"));
    }

    fn it_overrides_settings_from_env<F>(make_settings: F)
    where
        F: FnOnce() -> Result<Settings, ConfigError>,
    {
        env::set_var("LISTENER__TCP__ADDRESS", "10.0.0.1:1883");
        env::set_var("LISTENER__TLS__ADDRESS", "10.0.0.1:8883");
        env::set_var("LISTENER__TLS__CERTIFICATE", "/tmp/edgehub/identity.pem");
        env::set_var("AUTH__BASE_URL", "/auth/");

        let settings = make_settings().unwrap();

        assert_eq!(
            settings.listener().tcp().unwrap().addr(),
            "10.0.0.1:1883"
        );
        assert_eq!(
            settings.listener().tls(),
            Some(&TlsTransportConfig::new(
                "10.0.0.1:8883",
                Some("/tmp/edgehub/identity.pem")
            ))
        );
        assert_eq!(settings.auth().url(), "http://localhost:7120/auth/");
    }
}
