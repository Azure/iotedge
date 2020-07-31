mod size;

pub use size::HumanSize;

use std::{num::NonZeroUsize, time::Duration};

use serde::Deserialize;

const DAYS: u64 = 24 * 60 * 60;

#[derive(Debug, Clone, Default, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct BrokerConfig {
    retained_messages: RetainedMessagesConfig,
    session: SessionConfig,
    persistence: Option<SessionPersistenceConfig>,
}

impl BrokerConfig {
    pub fn new(
        retained_messages: RetainedMessagesConfig,
        session: SessionConfig,
        persistence: Option<SessionPersistenceConfig>,
    ) -> Self {
        Self {
            retained_messages,
            session,
            persistence,
        }
    }

    pub fn retained_messages(&self) -> &RetainedMessagesConfig {
        &self.retained_messages
    }

    pub fn session(&self) -> &SessionConfig {
        &self.session
    }

    pub fn persistence(&self) -> Option<&SessionPersistenceConfig> {
        self.persistence.as_ref()
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

impl Default for SessionConfig {
    fn default() -> Self {
        SessionConfig::new(
            Duration::from_secs(60 * DAYS),
            Some(HumanSize::new_kilobytes(256).expect("256kb")),
            16,
            1000,
            Some(HumanSize::new_bytes(0)),
            QueueFullAction::DropNew,
        )
    }
}

#[derive(Debug, Deserialize, Clone, Copy, PartialEq)]
#[serde(rename_all = "snake_case")]
pub enum QueueFullAction {
    DropNew,
    DropOld,
}

#[derive(Debug, PartialEq, Clone, Deserialize)]
pub struct RetainedMessagesConfig {
    max_count: usize,
    #[serde(with = "humantime_serde")]
    expiration: Duration,
}

impl RetainedMessagesConfig {
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

impl Default for RetainedMessagesConfig {
    fn default() -> Self {
        RetainedMessagesConfig::new(1000, Duration::from_secs(60 * DAYS))
    }
}

// TODO: apply settings
#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct SessionPersistenceConfig {
    file_path: String,
    #[serde(with = "humantime_serde")]
    time_interval: Duration,
    unsaved_message_count: u32,
}
