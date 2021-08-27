use std::{path::Path, str::FromStr, time::Duration};

use config::{Config, ConfigError, Environment, File, FileFormat};
use serde::Deserialize;
use uuid::Uuid;

pub const DEFAULTS: &str = include_str!("../config/default.json");

#[derive(Debug, Clone, Deserialize)]
pub struct Settings {
    test_scenario: TestScenario,

    trc_url: String,

    tracking_id: Option<String>,

    batch_id: Option<String>,

    #[serde(with = "humantime_serde")]
    test_start_delay: Duration,

    #[serde(with = "humantime_serde")]
    message_frequency: Duration,

    message_size_in_bytes: u32,

    initiate_topic: String,

    relay_topic: String,

    messages_to_send: Option<u32>,

    iotedge_moduleid: String,
}

impl Settings {
    pub fn new() -> Result<Self, ConfigError> {
        let mut config = Config::new();

        config.merge(File::from_str(DEFAULTS, FileFormat::Json))?;
        config.merge(Environment::new())?;

        let test_scenario: TestScenario = config.get("test_scenario")?;
        match test_scenario {
            TestScenario::Initiate | TestScenario::InitiateAndReceiveRelayed => {
                config.set("batch_id", Some(Uuid::new_v4().to_string()))?;
            }
            _ => {}
        }

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

    pub fn tracking_id(&self) -> Option<String> {
        self.tracking_id.clone()
    }

    pub fn batch_id(&self) -> Option<Uuid> {
        self.batch_id.as_ref().map(|batch_id| {
            Uuid::from_str(batch_id)
                .expect("should be valid uuid as it cannot be changed once created")
        })
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

    pub fn initiate_topic(&self) -> String {
        self.initiate_topic.clone()
    }

    pub fn relay_topic(&self) -> String {
        self.relay_topic.clone()
    }

    pub fn messages_to_send(&self) -> Option<u32> {
        self.messages_to_send
    }

    pub fn module_name(&self) -> String {
        self.iotedge_moduleid.clone()
    }
}

#[derive(Debug, Clone, Deserialize)]
pub enum TestScenario {
    Initiate,
    InitiateAndReceiveRelayed,
    Relay,
    Receive,
}
