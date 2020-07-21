use std::path::Path;

use config::{Config, ConfigError, File, FileFormat};
use lazy_static::lazy_static;
use serde::Deserialize;

use mqtt_broker_core::settings::{BrokerConfig, TcpTransport, TlsTransport};

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
    tcp: Option<TcpTransport>,
    tls: Option<TlsTransport>,
}

impl ListenerConfig {
    pub fn tcp(&self) -> Option<&TcpTransport> {
        self.tcp.as_ref()
    }

    pub fn tls(&self) -> Option<&TlsTransport> {
        self.tls.as_ref()
    }
}

#[cfg(test)]
mod tests {
    use std::{path::Path, time::Duration};

    use mqtt_broker_core::settings::{
        BrokerConfig, HumanSize, QueueFullAction, RetainedMessages, SessionConfig, TcpTransport,
    };

    use super::{ListenerConfig, Settings};
    use matches::assert_matches;

    const DAYS: u64 = 24 * 60 * 60;

    #[test]
    fn it_loads_defaults() {
        let settings = Settings::default();

        assert_eq!(
            settings,
            Settings {
                listener: ListenerConfig {
                    tcp: Some(TcpTransport::new("0.0.0.0:1883")),
                    tls: None,
                },
                broker: BrokerConfig::new(
                    RetainedMessages::new(1000, Duration::from_secs(60 * DAYS)),
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
    fn it_overrides_defaults() {
        let settings = Settings::from_file(Path::new("test/config_correct.json"))
            .expect("should be able to create instance from configuration file");

        assert_eq!(
            settings.broker().retained_messages().expiration(),
            Duration::from_secs(90 * DAYS)
        );
    }

    #[test]
    fn it_refuses_persistence_with_no_file_path() {
        let settings = Settings::from_file(Path::new("test/config_no_file_path.json"));

        assert_matches!(settings, Err(_));
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
