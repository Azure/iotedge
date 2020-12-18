use std::path::Path;

use config::{Config, ConfigError, Environment, File, FileFormat};
use serde::Deserialize;

pub const DEFAULTS: &str = include_str!("../config/default.json");

#[derive(Debug, Clone)]
pub struct Settings {
    test_scenario: TestScenario,
    test_result_coordinator_url: String,
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

    pub fn test_result_coordinator_url(&self) -> &String {
        &self.test_result_coordinator_url
    }
}

impl<'de> serde::Deserialize<'de> for Settings {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        #[derive(Debug, Deserialize)]
        struct Inner {
            test_scenario: TestScenario,
            test_result_coordinator_url: String,
        }
        let Inner {
            test_scenario,
            test_result_coordinator_url,
        } = serde::Deserialize::deserialize(deserializer)?;

        Ok(Settings {
            test_scenario,
            test_result_coordinator_url,
        })
    }
}

#[derive(Debug, Clone, Deserialize)]
pub enum TestScenario {
    Relay,
    Initiate,
}
