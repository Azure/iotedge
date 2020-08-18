mod size;

pub use size::HumanSize;

use std::{num::NonZeroUsize, path::PathBuf, time::Duration};

use serde::Deserialize;

const DAYS: u64 = 24 * 60 * 60;

#[derive(Debug, Clone, Default, PartialEq, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct BrokerConfig {
    retained_messages: RetainedMessagesConfig,
    session: SessionConfig,
    persistence: SessionPersistenceConfig,
}

impl BrokerConfig {
    pub fn new(
        retained_messages: RetainedMessagesConfig,
        session: SessionConfig,
        persistence: SessionPersistenceConfig,
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

    pub fn persistence(&self) -> &SessionPersistenceConfig {
        &self.persistence
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

#[derive(Debug, Clone, PartialEq, Deserialize)]
pub struct SessionPersistenceConfig {
    path: PathBuf,
    #[serde(with = "humantime_serde")]
    time_interval: Duration,
}

impl SessionPersistenceConfig {
    pub fn new(path: PathBuf, time_interval: Duration) -> Self {
        Self {
            path,
            time_interval,
        }
    }

    pub fn file_path(&self) -> PathBuf {
        self.path.clone()
    }

    pub fn time_interval(&self) -> Duration {
        self.time_interval
    }
}

impl Default for SessionPersistenceConfig {
    fn default() -> Self {
        SessionPersistenceConfig::new(PathBuf::from("/tmp/mqttd/"), Duration::from_secs(300))
    }
}

/// This type is a Option-like wrapper around any type T. The primary goal is
/// to make config section to be enabled/disabled during desirialization.
#[derive(Debug, Clone, Deserialize)]
pub struct Enable<T> {
    enabled: Option<bool>,

    #[serde(flatten)]
    value: Option<T>,
}

impl<T> Enable<T> {
    fn new(enabled: Option<bool>, value: Option<T>) -> Self {
        Self { enabled, value }
    }

    pub fn enabled(value: T) -> Self {
        Self::new(Some(true), Some(value))
    }

    pub fn disabled() -> Self {
        Self::new(None, None)
    }

    /// Returns the borrowed reference to a stored value.
    ///
    /// When optional `enabled` flag is set to false, the value will be
    /// disabled and None returned. When `enabled` is not set or equals true, the
    /// value will return.
    pub fn as_inner(&self) -> Option<&T> {
        match (self.enabled, self.value.as_ref()) {
            (None, Some(value)) => Some(value),
            (Some(true), Some(value)) => Some(value),
            _ => None,
        }
    }
}

impl<T> From<Option<T>> for Enable<T> {
    fn from(value: Option<T>) -> Self {
        match value {
            Some(value) => Enable::enabled(value),
            None => Enable::disabled(),
        }
    }
}

impl<T: PartialEq> PartialEq for Enable<T> {
    fn eq(&self, other: &Self) -> bool {
        self.as_inner() == other.as_inner()
    }
}

#[cfg(test)]
mod tests {
    use serde::Deserialize;

    use super::Enable;

    #[test]
    fn it_returns_inner() {
        let value: Enable<()> = Enable::new(None, None);
        assert_eq!(value.as_inner(), None);

        let value: Enable<()> = Enable::new(Some(true), None);
        assert_eq!(value.as_inner(), None);

        let value: Enable<()> = Enable::new(Some(false), None);
        assert_eq!(value.as_inner(), None);

        let value: Enable<()> = Enable::new(None, Some(()));
        assert_eq!(value.as_inner(), Some(&()));

        let value: Enable<()> = Enable::new(Some(true), Some(()));
        assert_eq!(value.as_inner(), Some(&()));

        let value: Enable<()> = Enable::new(Some(false), Some(()));
        assert_eq!(value.as_inner(), None);
    }

    #[test]
    fn it_deserializes_as_option_when_some() {
        let json = serde_json::json!({ "foo": { "bar": 42 } });
        let value: Container = serde_json::from_value(json).unwrap();

        assert_eq!(value.foo(), Some(&Foo { bar: 42 }))
    }

    #[test]
    fn it_deserializes_as_option_when_none() {
        let json = serde_json::json!({ "foo": {  } });
        let value: Container = serde_json::from_value(json).unwrap();

        assert_eq!(value.foo(), None)
    }

    #[test]
    fn it_deserializes_as_option_when_enabled() {
        let json = serde_json::json!({ "foo": { "enabled": true, "bar": 42 } });
        let value: Container = serde_json::from_value(json).unwrap();

        assert_eq!(value.foo(), Some(&Foo { bar: 42 }))
    }

    #[test]
    fn it_deserializes_as_option_when_disabled() {
        let json = serde_json::json!({ "foo": { "enabled": false, "bar": 42 } });
        let value: Container = serde_json::from_value(json).unwrap();

        assert_eq!(value.foo(), None)
    }

    #[test]
    fn it_deserializes_as_option_when_missing() {
        let json = serde_json::json!({});
        let value: Container = serde_json::from_value(json).unwrap();

        assert_eq!(value.foo(), None)
    }

    #[derive(Debug, Deserialize)]
    struct Container {
        foo: Option<Enable<Foo>>,
    }

    impl Container {
        fn foo(&self) -> Option<&Foo> {
            self.foo.as_ref().and_then(Enable::as_inner)
        }
    }

    #[derive(Debug, PartialEq, Deserialize)]
    struct Foo {
        bar: i32,
    }
}
