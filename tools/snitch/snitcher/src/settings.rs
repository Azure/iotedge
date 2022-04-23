// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::default::Default;
use std::env;
use std::time::Duration;

use serde::Deserialize;
use url::Url;

use crate::error::{Error, ErrorKind, Result};

const DEFAULT_TEST_DURATION_SECS: u64 = 60 * 60 * 8;

const BUILD_ID_KEY: &str = "BUILD_ID";
const TEST_DURATION_IN_SECS_KEY: &str = "TEST_DURATION_IN_SECS";
const REPORTING_INTERVAL_IN_SECS_KEY: &str = "REPORTING_INTERVAL_IN_SECS";
const ALERT_URL_KEY: &str = "ALERT_URL";
const ANALYZER_URL_KEY: &str = "ANALYZER_URL";
const BLOB_STORAGE_ACCOUNT_KEY: &str = "BLOB_STORAGE_ACCOUNT";
const BLOB_STORAGE_MASTER_KEY_KEY: &str = "BLOB_STORAGE_MASTER_KEY";
const MANAGEMENT_URI_KEY: &str = "MANAGEMENT_URI";

static DEFAULT_SETTINGS: &str = include_str!("settings.yaml");

#[derive(Clone, Deserialize)]
pub struct Alert {
    scheme: String,
    host: String,
    path: String,
    query: HashMap<String, String>,
    #[serde(with = "url_serde")]
    url: Url,
}

impl Alert {
    pub fn scheme(&self) -> &str {
        &self.scheme
    }

    pub fn host(&self) -> &str {
        &self.host
    }

    pub fn path(&self) -> &str {
        &self.path
    }

    pub fn query(&self) -> &HashMap<String, String> {
        &self.query
    }

    pub fn url(&self) -> &Url {
        &self.url
    }
}

impl From<Url> for Alert {
    fn from(url: Url) -> Alert {
        Alert {
            scheme: url.scheme().to_owned(),
            host: url
                .host_str()
                .expect("Alert URL does not have a host component")
                .to_owned(),
            path: url.path().to_owned(),
            query: url
                .query_pairs()
                .map(|(k, v)| (k.into_owned(), v.into_owned()))
                .collect(),
            url,
        }
    }
}

#[derive(Clone, Deserialize)]
pub struct Settings {
    build_id: String,
    test_duration: Duration,
    alert: Alert,
    #[serde(with = "url_serde")]
    analyzer_url: Url,
    blob_storage_account: String,
    blob_storage_master_key: String,
    reporting_interval: Option<Duration>,
    #[serde(with = "url_serde")]
    management_uri: Url,
}

impl Default for Settings {
    fn default() -> Self {
        serde_yaml::from_str(DEFAULT_SETTINGS).expect("Failed to de-serialize default settings")
    }
}

fn get_env(key: &str) -> Result<String> {
    env::var(key).map_err(|_| Error::new(ErrorKind::Env(key.to_string())))
}

impl Settings {
    pub fn merge_env(mut self) -> Result<Self> {
        self.build_id = get_env(BUILD_ID_KEY)?;
        self.test_duration = Duration::from_secs(
            env::var(TEST_DURATION_IN_SECS_KEY)
                .map(|interval| interval.parse().unwrap_or(DEFAULT_TEST_DURATION_SECS))
                .unwrap_or(DEFAULT_TEST_DURATION_SECS),
        );
        self.alert = Alert::from(Url::parse(&get_env(ALERT_URL_KEY)?)?);
        self.analyzer_url = Url::parse(&get_env(ANALYZER_URL_KEY)?)?;
        self.blob_storage_account = get_env(BLOB_STORAGE_ACCOUNT_KEY)?;
        self.blob_storage_master_key = get_env(BLOB_STORAGE_MASTER_KEY_KEY)?;

        if let Ok(management_uri) = get_env(MANAGEMENT_URI_KEY) {
            self.management_uri = Url::parse(&management_uri)?;
        }

        self.reporting_interval = get_env(REPORTING_INTERVAL_IN_SECS_KEY)
            .ok()
            .and_then(|interval| interval.parse().ok())
            .map(Duration::from_secs);

        Ok(self)
    }

    pub fn build_id(&self) -> &str {
        &self.build_id
    }

    pub fn test_duration(&self) -> &Duration {
        &self.test_duration
    }

    pub fn alert(&self) -> &Alert {
        &self.alert
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

    pub fn reporting_interval(&self) -> Option<&Duration> {
        self.reporting_interval.as_ref()
    }

    pub fn management_uri(&self) -> &Url {
        &self.management_uri
    }
}
