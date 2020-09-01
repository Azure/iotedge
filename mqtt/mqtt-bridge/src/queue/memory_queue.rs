use std::cell::RefCell;
use std::collections::btree_map::Range;
use std::collections::BTreeMap;
use std::rc::Rc;
use std::sync::Arc;
use std::task::Waker;
use std::{iter::Iterator, time::Duration};

use anyhow::{Error, Result};
use async_trait::async_trait;
use mqtt3::proto::Publication;
// TODO REVIEW: do we need this tokio mutex
use tokio::sync::Mutex;

use crate::queue::{
    simple_message_loader::InMemoryMessageLoader, waking_map::WakingMap, Key, Queue, QueueError,
};

struct InMemoryQueue {
    state: Arc<Mutex<WakingMap>>,
    offset: u32,
}

#[async_trait]
impl<'a> Queue<'a> for InMemoryQueue {
    type Loader = InMemoryMessageLoader;

    fn new() -> Self {
        let state = WakingMap::new(BTreeMap::new());
        let state = Arc::new(Mutex::new(state));
        let offset = 0;
        InMemoryQueue { state, offset }
    }

    async fn insert(
        &mut self,
        priority: u32,
        ttl: Duration,
        message: Publication,
    ) -> Result<Key, QueueError> {
        let key = Key {
            offset: self.offset,
            priority,
            ttl,
        };

        let mut state_lock = self.state.lock().await;
        state_lock.insert(key.clone(), message);
        self.offset += 1;
        Ok(key)
    }

    async fn remove(&mut self, key: Key) -> Result<bool, QueueError> {
        let mut state_lock = self.state.lock().await;
        state_lock.remove(key).ok_or(QueueError::Removal())?;
        Ok(true)
    }

    async fn get_loader(&'a mut self, batch_size: usize) -> InMemoryMessageLoader {
        InMemoryMessageLoader::new(Arc::clone(&self.state), batch_size).await
    }
}

// TODO: test queue state
#[cfg(test)]
mod tests {
    use std::time::Duration;

    use bytes::Bytes;
    use futures_util::stream::StreamExt;
    use matches::assert_matches;
    use mqtt3::proto::{Publication, QoS};

    use crate::queue::QueueError;
    use crate::queue::{simple_queue::InMemoryQueue, Key, Queue};

    #[tokio::test]
    async fn insert() {
        // setup state
        let mut queue = InMemoryQueue::new();

        // setup data
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
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let pub2 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert some elements
        queue
            .insert(0, Duration::from_secs(5), pub1.clone())
            .await
            .unwrap();
        queue
            .insert(0, Duration::from_secs(5), pub2.clone())
            .await
            .unwrap();

        // init loader
        let batch_size: usize = 5;
        let mut loader = queue.get_loader(batch_size).await;

        // make sure same publications come out in correct order
        let extracted1 = loader.next().await.unwrap();
        let extracted2 = loader.next().await.unwrap();
        assert_eq!(extracted1.0, key1);
        assert_eq!(extracted2.0, key2);
        assert_eq!(extracted1.1, pub1);
        assert_eq!(extracted2.1, pub2);
    }

    #[tokio::test]
    async fn remove() {
        // setup state
        let mut queue = InMemoryQueue::new();

        // setup data
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
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let pub2 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert some elements
        queue
            .insert(0, Duration::from_secs(5), pub1.clone())
            .await
            .unwrap();

        // init loader
        let batch_size: usize = 1;
        let mut loader = queue.get_loader(batch_size).await;

        // process first message, forcing loader to get new batch on the next read
        loader.next().await.unwrap();
        queue.remove(key1).await.unwrap();

        // add a second message and verify this is returned first by loader
        queue
            .insert(0, Duration::from_secs(5), pub2.clone())
            .await
            .unwrap();
        let extracted = loader.next().await.unwrap();
        assert_eq!((extracted.0, extracted.1), (key2, pub2));
    }

    #[tokio::test]
    async fn remove_key_that_dne() {
        // setup state
        let mut queue = InMemoryQueue::new();

        // setup data
        let key1 = Key {
            priority: 0,
            offset: 0,
            ttl: Duration::from_secs(5),
        };

        let removal = queue.remove(key1).await;
        assert_matches!(removal, Err(QueueError::Removal()));
    }

    #[tokio::test]
    async fn get_loader_with_correct_batch_size() {
        // setup state
        let mut queue = InMemoryQueue::new();

        // setup data
        let key1 = Key {
            priority: 0,
            offset: 0,
            ttl: Duration::from_secs(5),
        };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let pub2 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert 2 elements
        queue
            .insert(0, Duration::from_secs(5), pub1.clone())
            .await
            .unwrap();
        queue
            .insert(0, Duration::from_secs(5), pub2.clone())
            .await
            .unwrap();

        // init loader with batch size 1
        let batch_size: usize = 1;
        let mut loader = queue.get_loader(batch_size).await;

        // the queue never removed any elements
        // therefore the loader should get the same element in the two batches of size 1
        let extracted1 = loader.next().await.unwrap();
        let extracted2 = loader.next().await.unwrap();
        assert_eq!(extracted1.0, key1);
        assert_eq!(extracted2.0, key1);
        assert_eq!(extracted1.1, pub1);
        assert_eq!(extracted2.1, pub1);
    }
}
