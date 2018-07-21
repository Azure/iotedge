// Copyright (c) Microsoft. All rights reserved.

use error::{Error, Result};
use std::default::Default;
use std::env;
use std::time::Duration;

use serde_yaml;
use url::Url;
use url_serde;

const DEFAULT_TEST_DURATION_SECS: u64 = 60 * 60 * 8;

const TEST_DURATION_IN_SECS_KEY: &str = "TEST_DURATION_IN_SECS";
const REPORTING_INTERVAL_IN_SECS_KEY: &str = "REPORTING_INTERVAL_IN_SECS";
const ALERT_URL_KEY: &str = "ALERT_URL";
const INFLUX_URL_KEY: &str = "INFLUX_URL";
const INFLUX_DB_NAME_KEY: &str = "INFLUX_DB_NAME";
const ANALYZER_URL_KEY: &str = "ANALYZER_URL";
const BLOB_STORAGE_ACCOUNT_KEY: &str = "BLOB_STORAGE_ACCOUNT";
const BLOB_STORAGE_MASTER_KEY_KEY: &str = "BLOB_STORAGE_MASTER_KEY";
const BLOB_CONTAINER_NAME_KEY: &str = "BLOB_CONTAINER_NAME";
const DOCKER_URL_KEY: &str = "DOCKER_URL";

static DEFAULT_SETTINGS: &str = include_str!("settings.yaml");

#[derive(Clone, Deserialize)]
pub struct Settings {
    test_duration: Duration,
    #[serde(with = "url_serde")]
    alert_url: Url,
    #[serde(with = "url_serde")]
    influx_url: Url,
    influx_db_name: String,
    #[serde(with = "url_serde")]
    analyzer_url: Url,
    blob_storage_account: String,
    blob_storage_master_key: String,
    blob_container_name: String,
    reporting_interval: Option<Duration>,
    #[serde(with = "url_serde")]
    docker_url: Url,
}

impl Default for Settings {
    fn default() -> Self {
        serde_yaml::from_str(DEFAULT_SETTINGS).expect("Failed to de-serialize default settings")
    }
}

fn get_env(key: &str) -> Result<String> {
    env::var(key).map_err(|_| Error::Env(key.to_string()))
}

impl Settings {
    pub fn merge_env(mut self) -> Result<Self> {
        self.test_duration = Duration::from_secs(
            env::var(TEST_DURATION_IN_SECS_KEY)
                .map(|interval| interval.parse().unwrap_or(DEFAULT_TEST_DURATION_SECS))
                .unwrap_or(DEFAULT_TEST_DURATION_SECS),
        );
        self.alert_url = Url::parse(&get_env(ALERT_URL_KEY)?)?;
        self.influx_url = Url::parse(&get_env(INFLUX_URL_KEY)?)?;
        self.influx_db_name = get_env(INFLUX_DB_NAME_KEY)?;
        self.analyzer_url = Url::parse(&get_env(ANALYZER_URL_KEY)?)?;
        self.blob_storage_account = get_env(BLOB_STORAGE_ACCOUNT_KEY)?;
        self.blob_storage_master_key = get_env(BLOB_STORAGE_MASTER_KEY_KEY)?;
        self.blob_container_name = get_env(BLOB_CONTAINER_NAME_KEY)?;

        if let Ok(docker_url) = get_env(DOCKER_URL_KEY) {
            self.docker_url = Url::parse(&docker_url)?;
        }

        self.reporting_interval = get_env(REPORTING_INTERVAL_IN_SECS_KEY)
            .ok()
            .and_then(|interval| interval.parse().ok())
            .map(Duration::from_secs);

        Ok(self)
    }

    pub fn test_duration(&self) -> &Duration {
        &self.test_duration
    }

    pub fn alert_url(&self) -> &Url {
        &self.alert_url
    }

    pub fn influx_url(&self) -> &Url {
        &self.influx_url
    }

    pub fn influx_db_name(&self) -> &str {
        &self.influx_db_name
    }

    pub fn analyzer_url(&self) -> &Url {
        &self.analyzer_url
    }

    pub fn blob_storage_account(&self) -> &str {
        &self.blob_storage_account
    }

    pub fn blob_storage_master_key(&self) -> &str {
        &self.blob_storage_master_key
    }

    pub fn blob_container_name(&self) -> &str {
        &self.blob_container_name
    }

    pub fn reporting_interval(&self) -> Option<&Duration> {
        self.reporting_interval.as_ref()
    }

    pub fn docker_url(&self) -> &Url {
        &self.docker_url
    }
}
