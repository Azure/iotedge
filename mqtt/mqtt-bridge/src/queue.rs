use std::cmp::Ordering;
use std::time::Duration;

use async_trait::async_trait;
use futures_util::stream::Stream;
use mqtt3::proto::Publication;
use thiserror::Error;

mod memory_loader;
mod memory_queue;
mod waking_map;

// Queue used in bridge.
#[async_trait]
trait Queue<'a> {
    type Loader: Stream;

    fn new() -> Self;

    async fn insert(
        &mut self,
        priority: u32,
        ttl: Duration,
        message: Publication,
    ) -> Result<Key, QueueError>;

    async fn remove(&mut self, key: Key) -> Result<bool, QueueError>;

    async fn loader(&'a mut self, batch_size: usize) -> Self::Loader;
}

// Keys used in the queue.
// Ordered by offset only.
// When we implement priority and ttl for iothub telemetry we can order by priority first.
// Unordered by ttl.
#[derive(Eq, Ord, PartialEq, Clone, Debug)]
pub struct Key {
    priority: u32,
    offset: u32,
    ttl: Duration,
}

impl PartialOrd for Key {
    fn partial_cmp(&self, other: &Key) -> Option<Ordering> {
        if self.offset < other.offset {
            Some(Ordering::Less)
        } else if self.offset == other.offset {
            Some(Ordering::Equal)
        } else {
            Some(Ordering::Greater)
        }
    }
}

#[derive(Debug, Error)]
pub enum QueueError {
    #[error("Failed to remove messages from queue")]
    Removal(),
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use crate::queue::Key;

    #[test]
    fn key_offset_ordering() {
        // ordered by offset
        let key1 = Key {
            priority: 0,
            offset: 0,
            ttl: Duration::from_secs(5),
        };
        let key2 = Key {
            priority: 0,
            offset: 1,
            ttl: Duration::from_secs(5),
        };
        assert!(key2 > key1);
        assert!(key2 != key1);
        assert!(key1 < key2);
    }

    #[test]
    fn key_priority_ordering() {
        // not ordered by priority
        let key1 = Key {
            priority: 0,
            offset: 0,
            ttl: Duration::from_secs(5),
        };
        let key2 = Key {
            priority: 1,
            offset: 0,
            ttl: Duration::from_secs(5),
        };
        assert_eq!(key1 > key2, false);
        assert_eq!(key1 < key2, false);
        assert!(key1 != key2);
    }

    // TODO REVIEW: Does this guarantee that btreemap is not ordered by ttl? Why this weird behavior with equals?
    #[test]
    fn key_ttl_ordering() {
        // not ordered by ttl
        let key1 = Key {
            priority: 0,
            offset: 0,
            ttl: Duration::from_secs(10),
        };
        let key2 = Key {
            priority: 0,
            offset: 0,
            ttl: Duration::from_secs(5),
        };
        assert_eq!(key1 > key2, false);
        assert_eq!(key1 < key2, false);
        assert!(key1 != key2);
    }
}
