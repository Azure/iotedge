use std::path::Path;

use config::{Config, ConfigError, Environment, File, FileFormat};
use serde::Deserialize;

pub const DEFAULTS: &str = include_str!("../config/default.json");

#[derive(Debug, Clone)]
pub struct Settings {
    test_scenario: TestScenario,
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
}

impl<'de> serde::Deserialize<'de> for Settings {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        #[derive(Debug, serde_derive::Deserialize)]
        struct Inner {
            test_scenario: TestScenario,
        }
        let Inner { test_scenario } = serde::Deserialize::deserialize(deserializer)?;

        Ok(Settings { test_scenario })
    }
}

#[derive(Debug, Clone, Deserialize)]
pub enum TestScenario {
    Relay,
    Initiate,
}
