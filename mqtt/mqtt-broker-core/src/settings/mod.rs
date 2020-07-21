mod size;

pub use size::HumanSize;

use std::{
    num::NonZeroUsize,
    path::{Path, PathBuf},
    time::Duration,
};

use config::{Config, File, FileFormat};
use lazy_static::lazy_static;
use serde::Deserialize;

pub const DEFAULTS: &str = include_str!("../../config/default.json");

lazy_static! {
    static ref DEFAULT_BROKER_CONFIG: BrokerConfig = {
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
pub struct BrokerConfig {
    retained_messages: RetainedMessages,
    session: SessionConfig,
    persistence: Option<SessionPersistence>,
}

impl BrokerConfig {
    pub fn new(
        retained_messages: RetainedMessages,
        session: SessionConfig,
        persistence: Option<SessionPersistence>,
    ) -> Self {
        Self {
            retained_messages,
            session,
            persistence,
        }
    }

    // pub fn from_file<P: AsRef<Path>>(path: P) -> Result<Self, ConfigError> {
    //     let mut s = Config::new();
    //     s.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
    //     s.merge(File::from(path.as_ref()))?;

    //     s.try_into()
    // }

    pub fn retained_messages(&self) -> &RetainedMessages {
        &self.retained_messages
    }

    pub fn session(&self) -> &SessionConfig {
        &self.session
    }

    pub fn persistence(&self) -> Option<&SessionPersistence> {
        self.persistence.as_ref()
    }
}

impl Default for BrokerConfig {
    fn default() -> Self {
        DEFAULT_BROKER_CONFIG.clone()
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct ListenerConfig {
    tcp: Option<TcpTransport>,
    tls: Option<TlsTransport>,
}

// impl Default for ListenerConfig {
//     fn default() -> Self {
//         DEFAULT_BROKER_CONFIG.transports.clone()
//     }
// }

impl ListenerConfig {
    pub fn tcp(&self) -> Option<&TcpTransport> {
        self.tcp.as_ref()
    }

    pub fn tls(&self) -> Option<&TlsTransport> {
        self.tls.as_ref()
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct TcpTransport {
    #[serde(rename = "address")]
    addr: String,
}

impl TcpTransport {
    pub fn new(addr: impl Into<String>) -> Self {
        Self { addr: addr.into() }
    }

    pub fn addr(&self) -> &str {
        &self.addr
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct TlsTransport {
    #[serde(rename = "address")]
    addr: String,

    #[serde(rename = "certificate")]
    cert_path: Option<PathBuf>,
}

impl TlsTransport {
    pub fn new(addr: impl Into<String>, cert_path: Option<PathBuf>) -> Self {
        Self {
            addr: addr.into(),
            cert_path,
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
pub struct SessionConfig {
    #[serde(with = "humantime_serde")]
    expiration: Duration,
    max_message_size: Option<HumanSize>,
    max_inflight_messages: usize,
    max_queued_messages: usize,
    max_queued_size: Option<HumanSize>,
    when_full: QueueFullAction,
}

impl SessionConfig {
    pub fn new(
        expiration: Duration,
        max_message_size: Option<HumanSize>,
        max_inflight_messages: usize,
        max_queued_messages: usize,
        max_queued_size: Option<HumanSize>,
        when_full: QueueFullAction,
    ) -> Self {
        Self {
            expiration,
            max_message_size,
            max_inflight_messages,
            max_queued_messages,
            max_queued_size,
            when_full,
        }
    }

    pub fn max_message_size(&self) -> Option<NonZeroUsize> {
        self.max_message_size
            .and_then(|size| NonZeroUsize::new(size.get()))
    }

    pub fn max_inflight_messages(&self) -> Option<NonZeroUsize> {
        NonZeroUsize::new(self.max_inflight_messages)
    }

    pub fn max_queued_messages(&self) -> Option<NonZeroUsize> {
        NonZeroUsize::new(self.max_queued_messages)
    }

    pub fn max_queued_size(&self) -> Option<NonZeroUsize> {
        self.max_queued_size
            .and_then(|size| NonZeroUsize::new(size.get()))
    }

    pub fn when_full(&self) -> QueueFullAction {
        self.when_full
    }
}

// impl Default for SessionConfig {
//     fn default() -> Self {
//         DEFAULT_BROKER_CONFIG.session().clone()
//     }
// }

#[derive(Debug, Deserialize, Clone, Copy, PartialEq)]
#[serde(rename_all = "snake_case")]
pub enum QueueFullAction {
    DropNew,
    DropOld,
}

#[derive(Debug, PartialEq, Clone, Deserialize)]
pub struct RetainedMessages {
    max_count: usize,
    #[serde(with = "humantime_serde")]
    expiration: Duration,
}

impl RetainedMessages {
    pub fn new(max_count: usize, expiration: Duration) -> Self {
        Self {
            max_count,
            expiration,
        }
    }

    pub fn max_count(&self) -> Option<NonZeroUsize> {
        NonZeroUsize::new(self.max_count)
    }

    pub fn expiration(&self) -> Duration {
        self.expiration
    }
}

// impl Default for RetainedMessages {
//     fn default() -> Self {
//         DEFAULT_BROKER_CONFIG.retained_messages().clone()
//     }
// }

// TODO: apply settings
#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct SessionPersistence {
    file_path: String,
    #[serde(with = "humantime_serde")]
    time_interval: Duration,
    unsaved_message_count: u32,
}

#[cfg(test)]
mod tests {
    use std::{path::Path, time::Duration};

    use matches::assert_matches;

    use super::{
        BrokerConfig, HumanSize, ListenerConfig, QueueFullAction, RetainedMessages, SessionConfig,
        TcpTransport,
    };

    const DAYS: u64 = 24 * 60 * 60;

    #[test]
    fn it_loads_defaults() {
        let settings = BrokerConfig::default();

        assert_eq!(
            settings,
            BrokerConfig {
                listener: ListenerConfig {
                    tcp: Some(TcpTransport {
                        addr: "0.0.0.0:1883".into(),
                    }),
                    tls: None,
                },
                retained_messages: RetainedMessages {
                    expiration: Duration::from_secs(60 * DAYS),
                    max_count: 1000
                },
                session: SessionConfig {
                    expiration: Duration::from_secs(60 * DAYS),
                    max_message_size: Some(HumanSize::new_kilobytes(256).expect("256kb")),
                    max_inflight_messages: 16,
                    max_queued_messages: 1000,
                    max_queued_size: Some(HumanSize::new_bytes(0)),
                    when_full: QueueFullAction::DropNew,
                },
                persistence: None
            }
        );
    }

    #[test]
    fn it_overrides_defaults() {
        let settings = BrokerConfig::from_file(Path::new("test/config_correct.json"))
            .expect("should be able to create instance from configuration file");

        assert_eq!(
            settings.retained_messages.expiration,
            Duration::from_secs(90 * 24 * 60 * 60)
        );
    }

    #[test]
    fn it_refuses_persistence_with_no_file_path() {
        let settings = BrokerConfig::from_file(Path::new("test/config_no_file_path.json"));

        assert_matches!(settings, Err(_));
    }

    #[test]
    fn it_type_mismatch_fails() {
        let settings = BrokerConfig::from_file(Path::new("test/config_bad_value_type.json"));

        assert_matches!(settings, Err(_));
    }

    #[test]
    fn it_overrides_messages_settings_with_zero() {
        let settings = BrokerConfig::from_file(Path::new("test/config_override_with_zero.json"))
            .expect("should be able to override settings with zero");

        assert_eq!(settings.session().max_message_size(), None);
        assert_eq!(settings.session().max_inflight_messages(), None);
        assert_eq!(settings.session().max_queued_size(), None);
        assert_eq!(settings.session().max_queued_messages(), None);
    }
}
