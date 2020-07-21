mod size;

pub use size::HumanSize;

use std::{
    num::NonZeroUsize,
    path::{Path, PathBuf},
    time::Duration,
};

use serde::Deserialize;

#[derive(Debug, Clone, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct BrokerConfig {
    retained_messages: RetainedMessages,
    session: SessionConfig,
    persistence: Option<SessionPersistence>,
}

impl Default for BrokerConfig {
    fn default() -> Self {
        todo!()
    }
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

// TODO: apply settings
#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct SessionPersistence {
    file_path: String,
    #[serde(with = "humantime_serde")]
    time_interval: Duration,
    unsaved_message_count: u32,
}
