use std::{path::Path, time::Duration};

use config::{Config, ConfigError, Environment, File, FileFormat};
use serde::Deserialize;

pub const DEFAULTS: &str = include_str!("../config/default.json");

#[derive(Debug, Clone, Deserialize)]
pub struct Settings {
    test_scenario: TestScenario,

    trc_url: String,

    tracking_id: String,

    batch_id: String,

    #[serde(with = "humantime_serde")]
    test_start_delay: Duration,

    #[serde(with = "humantime_serde")]
    message_frequency: Duration,

    message_size_in_bytes: u32,

    topic: String,
}

impl Settings {
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

    pub fn test_scenario(&self) -> &TestScenario {
        &self.test_scenario
    }

    pub fn trc_url(&self) -> String {
        self.trc_url.clone()
    }

    pub fn tracking_id(&self) -> String {
        self.tracking_id.clone()
    }

    pub fn batch_id(&self) -> String {
        self.batch_id.clone()
    }

    pub fn message_frequency(&self) -> Duration {
        self.message_frequency
    }

    pub fn test_start_delay(&self) -> Duration {
        self.test_start_delay
    }

    pub fn message_size_in_bytes(&self) -> u32 {
        self.message_size_in_bytes
    }

    pub fn topic(&self) -> String {
        self.topic.clone()
    }
}

#[derive(Debug, Clone, Deserialize)]
pub enum TestScenario {
    Relay,
    Initiate,
}
