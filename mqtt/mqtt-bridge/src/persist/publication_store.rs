use std::sync::Arc;

use anyhow::Result;
use mqtt3::proto::Publication;
use parking_lot::Mutex;
use tracing::debug;

use crate::persist::{
    loader::MessageLoader,
    waking_state::StreamWakeableState,
    Key,
    PersistError, //WakingMemoryStore,
};

use super::storage::{ring_buffer::RingBuffer /*, sled::Sled*/, FlushOptions};

/// Pattern allows for the wrapping `PublicationStore` to be cloned and have non mutable methods
/// This facilitates sharing between multiple futures in a single threaded environment
struct PublicationStoreInner<S> {
    state: Arc<S>,
    offset: u64,
    loader: MessageLoader<S>,
}
/// Persistence implementation used for the bridge
pub struct PublicationStore<S>(Arc<Mutex<PublicationStoreInner<S>>>);

unsafe impl<S> Send for PublicationStore<S> {}

// impl PublicationStore<WakingMemoryStore> {
//     pub fn new_memory(batch_size: usize) -> PublicationStore<WakingMemoryStore> {
//         Self::new(WakingMemoryStore::default(), batch_size)
//     }
// }

// TODO: Sled
// impl PublicationStore<Sled> {
//     pub fn new_db(
//         path: String,
//         tree_name: String,
//         flush_options: FlushOptions,
//         batch_size: usize,
//     ) -> Self {
//         Self::new(Sled::new(path, tree_name, flush_options), batch_size)
//     }
// }

impl PublicationStore<RingBuffer> {
    pub fn new_ring_buffer(
        file_name: String,
        max_file_size: usize,
        flush_options: FlushOptions,
        batch_size: usize,
    ) -> Self {
        Self::new(
            RingBuffer::new(file_name, max_file_size, flush_options),
            batch_size,
        )
    }
}

impl<S> PublicationStore<S>
where
    S: StreamWakeableState,
{
    pub fn new(state: S, batch_size: usize) -> Self {
        let state = Arc::new(state);
        let loader = MessageLoader::new(state.clone(), batch_size);

        let offset = 0;
        let inner = PublicationStoreInner {
            state,
            offset,
            loader,
        };
        let inner = Arc::new(Mutex::new(inner));

        Self(inner)
    }

    async fn insert_into_store(
        pub_store: Arc<Mutex<PublicationStoreInner<S>>>,
        key: Key,
        value: Publication,
    ) -> Result<(), PersistError> {
        let state;
        {
            let lock = pub_store.lock();
            state = lock.state.clone();
        }
        state.insert(key, value).await
    }

    pub async fn push(&self, message: Publication) -> Result<Key, PersistError> {
        let offset;
        {
            let inner_borrow = self.0.lock();
            offset = inner_borrow.offset;
        }
        debug!(
            "persisting publication on topic {} with offset {}",
            message.topic_name, offset
        );

        let key = Key { offset };

        Self::insert_into_store(self.0.clone(), key, message).await?;

        {
            let mut inner_borrow = self.0.lock();
            inner_borrow.offset += 1;
        }
        Ok(key)
    }

    async fn remove_from_store(
        pub_store: Arc<Mutex<PublicationStoreInner<S>>>,
        key: Key,
    ) -> Result<(), PersistError> {
        let state;
        {
            let lock = pub_store.lock();
            state = lock.state.clone();
        }
        state.remove(key).await
    }

    pub async fn remove(&self, key: Key) -> Result<(), PersistError> {
        debug!("removing publication with key {:?}", key);

        Self::remove_from_store(self.0.clone(), key).await
    }

    pub fn loader(&self) -> MessageLoader<S> {
        let inner = self.0.lock();
        inner.loader.clone()
    }
}

impl<S: StreamWakeableState> Clone for PublicationStore<S> {
    fn clone(&self) -> Self {
        Self(self.0.clone())
    }
}

#[cfg(test)]
mod tests {
    use async_trait::async_trait;
    use bytes::Bytes;
    use futures_util::stream::TryStreamExt;
    use matches::assert_matches;
    use mqtt3::proto::{Publication, QoS};
    use rand::{distributions::Alphanumeric, thread_rng, Rng};
    use std::{
        collections::VecDeque,
        fs::{remove_dir_all, remove_file},
        path::PathBuf,
    };

    use crate::persist::{
        publication_store::PublicationStore,
        storage::{ring_buffer::RingBuffer, FlushOptions},
        Key, PersistError, StreamWakeableState,
    };

    const FLUSH_OPTIONS: FlushOptions = FlushOptions::Off;
    const FILE_NAME: &'static str = "test_file";
    const MAX_FILE_SIZE: usize = 1024 * 1024;

    fn cleanup_test_file(file_name: String) {
        let path = &PathBuf::from(file_name);
        if path.exists() {
            if path.is_file() {
                let result = remove_file(path);
                assert!(result.is_ok());
            }
            if path.is_dir() {
                let result = remove_dir_all(path);
                assert!(result.is_ok());
            }
        }
    }

    fn create_rand_str() -> String {
        thread_rng()
            .sample_iter(&Alphanumeric)
            .take(10)
            .map(char::from)
            .collect()
    }

    struct TestRingBuffer(RingBuffer, String);

    #[async_trait]
    impl StreamWakeableState for TestRingBuffer {
        async fn insert(&self, key: Key, value: Publication) -> Result<(), PersistError> {
            self.0.insert(key, value).await
        }

        async fn batch(&self, count: usize) -> Result<VecDeque<(Key, Publication)>, PersistError> {
            self.batch(count).await
        }

        async fn remove(&self, key: Key) -> Result<(), PersistError> {
            self.0.remove(key).await
        }

        fn set_waker(&mut self, waker: &std::task::Waker) {
            self.0.set_waker(waker)
        }
    }

    impl Default for TestRingBuffer {
        fn default() -> Self {
            let file_name = FILE_NAME.to_owned() + &create_rand_str();
            let rb = RingBuffer::new(file_name.clone(), MAX_FILE_SIZE, FLUSH_OPTIONS);
            TestRingBuffer(rb, file_name.clone())
        }
    }

    impl Drop for TestRingBuffer {
        fn drop(&mut self) {
            cleanup_test_file(self.1.clone())
        }
    }

    #[tokio::test]
    async fn insert() {
        // setup state
        let state = TestRingBuffer::default();
        let batch_size: usize = 5;
        let persistence = PublicationStore::new(state, batch_size);

        // setup data
        let key1 = Key { offset: 0 };
        let key2 = Key { offset: 1 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let pub2 = Publication {
            topic_name: "2".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert some elements
        persistence.push(pub1.clone()).await.unwrap();
        persistence.push(pub2.clone()).await.unwrap();

        // get loader
        let mut loader = persistence.loader();

        // make sure same publications come out in correct order
        let extracted1 = loader.try_next().await.unwrap().unwrap();
        let extracted2 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.0, key1);
        assert_eq!(extracted2.0, key2);
        assert_eq!(extracted1.1, pub1);
        assert_eq!(extracted2.1, pub2);
    }

    #[tokio::test]
    async fn remove() {
        // setup state
        let state = TestRingBuffer::default();
        let batch_size: usize = 1;
        let persistence = PublicationStore::new(state, batch_size);

        // setup data
        let key2 = Key { offset: 1 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let pub2 = Publication {
            topic_name: "2".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert some elements
        persistence.push(pub1.clone()).await.unwrap();

        // get loader
        let mut loader = persistence.loader();

        // process first message, forcing loader to get new batch on the next read
        let (key1, _) = loader.try_next().await.unwrap().unwrap();
        assert_matches!(persistence.remove(key1).await, Ok(_));

        // add a second message and verify this is returned by loader
        persistence.push(pub2.clone()).await.unwrap();
        let extracted = loader.try_next().await.unwrap().unwrap();
        assert_eq!((extracted.0, extracted.1), (key2, pub2));
    }

    #[tokio::test]
    async fn remove_key_inserted_but_not_retrieved() {
        // setup state
        let state = TestRingBuffer::default();
        let batch_size: usize = 1;
        let persistence = PublicationStore::new(state, batch_size);

        // setup data
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // can't remove an element that hasn't been seen
        persistence.push(pub1).await.unwrap();
        let removed = persistence.remove(key1).await;
        assert_matches!(removed, Err(_));
    }

    #[tokio::test]
    async fn remove_key_dne() {
        // setup state
        let state = TestRingBuffer::default();
        let batch_size: usize = 1;
        let persistence = PublicationStore::new(state, batch_size);

        // setup data
        let key1 = Key { offset: 0 };

        // verify failed removal
        let removal = persistence.remove(key1).await;
        assert_matches!(removal, Err(_));
    }

    #[tokio::test]
    async fn get_loader() {
        // setup state
        let state = TestRingBuffer::default();
        let batch_size: usize = 1;
        let persistence = PublicationStore::new(state, batch_size);

        // setup data
        let key1 = Key { offset: 0 };
        let key2 = Key { offset: 1 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let pub2 = Publication {
            topic_name: "2".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert 2 elements
        persistence.push(pub1.clone()).await.unwrap();
        persistence.push(pub2.clone()).await.unwrap();

        // get loader with batch size
        let mut loader = persistence.loader();

        // verify the loader returns both elements
        let extracted1 = loader.try_next().await.unwrap().unwrap();
        let extracted2 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.0, key1);
        assert_eq!(extracted2.0, key2);
        assert_eq!(extracted1.1, pub1);
        assert_eq!(extracted2.1, pub2);
    }
}
