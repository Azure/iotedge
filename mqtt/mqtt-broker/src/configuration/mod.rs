mod size;

pub use size::HumanSize;

use std::{
    convert::From,
    num::NonZeroUsize,
    path::{Path, PathBuf},
    time::Duration,
};

use config::{Config, ConfigError, File, FileFormat};
use serde::Deserialize;

pub const DEFAULTS: &str = include_str!("../../config/default.json");

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum Transport {
    Tcp {
        address: String,
    },
    Tls {
        address: String,
        certificate: PathBuf,
    },
}

#[derive(Debug, Deserialize, Clone, Copy, PartialEq)]
#[serde(rename_all = "snake_case")]
pub enum QueueFullAction {
    DropNew,
    DropOld,
}

#[derive(Debug, Deserialize, Clone)]
pub struct RetainedMessages {
    max_count: u32,
    #[serde(with = "humantime_serde")]
    expiration: Duration,
}

#[derive(Debug, Deserialize, Clone)]
pub struct SessionPersistence {
    file_path: String,
    #[serde(with = "humantime_serde")]
    time_interval: Duration,
    unsaved_message_count: u32,
}

#[derive(Debug, Deserialize, Clone, PartialEq)]
pub struct SessionConfig {
    #[serde(with = "humantime_serde")]
    expiration: Duration,
    max_message_size: Option<HumanSize>,
    max_inflight_messages: usize,
    max_queued_messages: usize,
    max_queued_size: Option<HumanSize>,
    when_full: QueueFullAction,
}

#[allow(unused_variables)]
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

#[derive(Debug, Deserialize, Clone)]
pub struct BrokerConfig {
    transports: Vec<Transport>,
    retained_messages: RetainedMessages,
    session: SessionConfig,
    persistence: Option<SessionPersistence>,
}

impl BrokerConfig {
    pub fn transports(&self) -> &Vec<Transport> {
        &self.transports
    }

    pub fn session(&self) -> &SessionConfig {
        &self.session
    }
}

impl BrokerConfig {
    pub fn from_file<P: AsRef<Path>>(path: P) -> Result<Self, ConfigError> {
        let mut s = Config::new();
        s.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        s.merge(File::from(path.as_ref()))?;

        s.try_into()
    }

    pub fn persistence(&self) -> Option<&SessionPersistence> {
        self.persistence.as_ref()
    }
}

impl Default for BrokerConfig {
    fn default() -> Self {
        let mut s = Config::new();

        // Returning Self instead of Result simplifies the code significantly.
        // It is guaranteed that next two calls must not fail,
        // otherwise we have a bug in the code or in ../config/default.json file.
        // It is guarded by a unit test as well.
        s.merge(File::from_str(DEFAULTS, FileFormat::Json)).expect(
            "Unable to load default broker config. Check default.json has invalid json format.",
        );
        s.try_into()
            .expect("Unable to load default broker config. Check default.json to match BrokerConfig structure.")
    }
}

#[cfg(test)]
mod tests {
    use std::{path::Path, time::Duration};

    use matches::assert_matches;

    use super::BrokerConfig;

    #[test]
    fn it_loads_defaults() {
        let settings = BrokerConfig::default();

        assert_eq!(
            settings.retained_messages.expiration,
            Duration::from_secs(60 * 24 * 60 * 60)
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
}
