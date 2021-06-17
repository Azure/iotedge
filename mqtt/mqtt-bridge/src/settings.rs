use std::{
    num::{NonZeroU64, NonZeroUsize},
    path::PathBuf,
    time::Duration,
    vec::Vec,
};

use serde::{Deserialize, Deserializer};

use mqtt_util::{CredentialProviderSettings, Credentials};

use crate::persist::FlushOptions;

const DEFAULT_UPSTREAM_PORT: &str = "8883";

#[derive(Debug, Clone, PartialEq)]
pub struct BridgeSettings {
    upstream: Option<ConnectionSettings>,
    remotes: Vec<ConnectionSettings>,
    storage: StorageSettings,
}

impl BridgeSettings {
    pub fn new(
        upstream: Option<ConnectionSettings>,
        remotes: Vec<ConnectionSettings>,
        storage: StorageSettings,
    ) -> Self {
        BridgeSettings {
            upstream,
            remotes,
            storage,
        }
    }

    pub fn upstream(&self) -> Option<&ConnectionSettings> {
        self.upstream.as_ref()
    }

    pub fn remotes(&self) -> &Vec<ConnectionSettings> {
        &self.remotes
    }

    pub fn storage(&self) -> &StorageSettings {
        &self.storage
    }
}

impl<'de> serde::Deserialize<'de> for BridgeSettings {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        #[derive(Debug, serde_derive::Deserialize)]
        struct Inner {
            #[serde(flatten)]
            nested_bridge: Option<CredentialProviderSettings>,
            upstream: UpstreamSettings,
            remotes: Vec<ConnectionSettings>,
            storage: StorageSettings,
        }

        let Inner {
            nested_bridge,
            upstream,
            remotes,
            storage,
        } = serde::Deserialize::deserialize(deserializer)?;

        let upstream_connection_settings = nested_bridge.map(|nested_bridge| ConnectionSettings {
            name: "$upstream".into(),
            address: format!(
                "{}:{}",
                nested_bridge.gateway_hostname(),
                DEFAULT_UPSTREAM_PORT
            ),
            subscriptions: upstream.subscriptions,
            credentials: Credentials::Provider(nested_bridge),
            clean_session: upstream.clean_session,
            keep_alive: upstream.keep_alive,
        });

        Ok(BridgeSettings {
            upstream: upstream_connection_settings,
            remotes,
            storage,
        })
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct ConnectionSettings {
    name: String,
    address: String,

    #[serde(flatten)]
    credentials: Credentials,
    subscriptions: Vec<Direction>,

    #[serde(with = "humantime_serde")]
    keep_alive: Duration,
    clean_session: bool,
}

impl ConnectionSettings {
    pub fn new(
        name: impl Into<String>,
        address: impl Into<String>,
        credentials: Credentials,
        subscriptions: Vec<Direction>,
        keep_alive: Duration,
        clean_session: bool,
    ) -> Self {
        Self {
            name: name.into(),
            address: address.into(),
            credentials,
            subscriptions,
            keep_alive,
            clean_session,
        }
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn address(&self) -> &str {
        &self.address
    }

    pub fn credentials(&self) -> &Credentials {
        &self.credentials
    }

    pub fn subscriptions(&self) -> Vec<TopicRule> {
        self.subscriptions
            .iter()
            .filter_map(|sub| match sub {
                Direction::In(topic) | Direction::Both(topic) => Some(topic.clone()),
                _ => None,
            })
            .collect()
    }

    pub fn forwards(&self) -> Vec<TopicRule> {
        self.subscriptions
            .iter()
            .filter_map(|sub| match sub {
                Direction::Out(topic) | Direction::Both(topic) => Some(topic.clone()),
                _ => None,
            })
            .collect()
    }

    pub fn keep_alive(&self) -> Duration {
        self.keep_alive
    }

    pub fn clean_session(&self) -> bool {
        self.clean_session
    }
}

#[derive(Debug, Default, Clone, PartialEq, Deserialize)]
pub struct TopicRule {
    topic: String,

    #[serde(rename = "outPrefix")]
    out_prefix: Option<String>,

    #[serde(rename = "inPrefix")]
    in_prefix: Option<String>,
}

impl TopicRule {
    pub fn new(
        topic: impl Into<String>,
        in_prefix: Option<String>,
        out_prefix: Option<String>,
    ) -> Self {
        Self {
            topic: topic.into(),
            out_prefix,
            in_prefix,
        }
    }

    pub fn topic(&self) -> &str {
        &self.topic
    }

    pub fn out_prefix(&self) -> Option<&str> {
        self.out_prefix.as_deref().filter(|s| !s.is_empty())
    }

    pub fn in_prefix(&self) -> Option<&str> {
        self.in_prefix.as_deref().filter(|s| !s.is_empty())
    }

    pub fn subscribe_to(&self) -> String {
        match &self.in_prefix {
            Some(local) => {
                format!("{}{}", local, self.topic)
            }
            None => self.topic.clone(),
        }
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(tag = "direction")]
pub enum Direction {
    #[serde(rename = "in")]
    In(TopicRule),

    #[serde(rename = "out")]
    Out(TopicRule),

    #[serde(rename = "both")]
    Both(TopicRule),
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
struct UpstreamSettings {
    #[serde(with = "humantime_serde")]
    keep_alive: Duration,
    clean_session: bool,
    subscriptions: Vec<Direction>,
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(tag = "type")]
pub enum StorageSettings {
    #[serde(rename = "memory")]
    Memory(MemorySettings),

    #[serde(rename = "ring_buffer")]
    RingBuffer(RingBufferSettings),
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct MemorySettings {
    #[serde(deserialize_with = "deserialize_nonzerouusize")]
    max_size: NonZeroUsize,
}

impl MemorySettings {
    pub fn new(max_size: NonZeroUsize) -> Self {
        Self { max_size }
    }

    pub fn max_size(&self) -> NonZeroUsize {
        self.max_size
    }
}

#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct RingBufferSettings {
    #[serde(deserialize_with = "deserialize_nonzerou64")]
    max_file_size: NonZeroU64,
    directory: PathBuf,
    flush_options: FlushOptions,
}

impl RingBufferSettings {
    pub fn new(max_file_size: NonZeroU64, directory: PathBuf, flush_options: FlushOptions) -> Self {
        Self {
            max_file_size,
            directory,
            flush_options,
        }
    }

    pub fn max_file_size(&self) -> NonZeroU64 {
        self.max_file_size
    }

    pub fn directory(&self) -> &PathBuf {
        &self.directory
    }

    pub fn flush_options(&self) -> &FlushOptions {
        &self.flush_options
    }
}

fn deserialize_nonzerou64<'de, D>(deserializer: D) -> Result<NonZeroU64, D::Error>
where
    D: Deserializer<'de>,
{
    let value = match serde_json::value::Value::deserialize(deserializer)? {
        serde_json::value::Value::String(value) => value.parse::<u64>().map_err(|err| {
            serde::de::Error::custom(format!("Cannot parse string value into u64: {}", err))
        })?,
        serde_json::value::Value::Number(value) => value.as_u64().ok_or_else(|| {
            serde::de::Error::custom(format!("Cannot parse numeric value {}", value))
        })?,
        _ => {
            return Err(serde::de::Error::custom(
                "Cannot parse value: wrong type, expected String or Number",
            ))
        }
    };
    NonZeroU64::new(value).ok_or_else(|| {
        serde::de::Error::custom(format!(
            "Cannot parse numeric value {} into NonZeroU64",
            value
        ))
    })
}

fn deserialize_nonzerouusize<'de, D>(deserializer: D) -> Result<NonZeroUsize, D::Error>
where
    D: Deserializer<'de>,
{
    #[allow(clippy::cast_possible_truncation)]
    let value = match serde_json::value::Value::deserialize(deserializer)? {
        serde_json::value::Value::String(value) => value.parse::<usize>().map_err(|err| {
            serde::de::Error::custom(format!("Cannot parse string value into usize: {}", err))
        })?,
        serde_json::value::Value::Number(value) => value.as_u64().ok_or_else(|| {
            serde::de::Error::custom(format!("Cannot parse numeric value {}", value))
        })? as usize,
        _ => {
            return Err(serde::de::Error::custom(
                "Cannot parse value: wrong type, expected String or Number",
            ))
        }
    };
    NonZeroUsize::new(value).ok_or_else(|| {
        serde::de::Error::custom(format!(
            "Cannot parse numeric value {} into NonZeroUsize",
            value
        ))
    })
}
