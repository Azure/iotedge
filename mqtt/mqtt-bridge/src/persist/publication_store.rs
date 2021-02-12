use std::{num::NonZeroUsize, sync::Arc};

use mqtt3::proto::Publication;
use parking_lot::Mutex;
use tracing::debug;

use crate::{
    persist::{
        loader::MessageLoader,
        waking_state::{memory::WakingMemoryStore, ring_buffer::RingBuffer, StreamWakeableState},
        Key, PersistResult,
    },
    settings::{MemorySettings, RingBufferSettings},
};

use crate::persist::StorageError;

/// Pattern allows for the wrapping `PublicationStore` to be cloned and have non mutable methods
/// This facilitates sharing between multiple futures in a single threaded environment
struct PublicationStoreInner<S> {
    state: Arc<Mutex<S>>,
    loader: MessageLoader<S>,
}
/// Persistence implementation used for the bridge
pub struct PublicationStore<S>(Arc<Mutex<PublicationStoreInner<S>>>);

impl PublicationStore<WakingMemoryStore> {
    pub fn new_memory(
        batch_size: NonZeroUsize,
        memory_settings: &MemorySettings,
    ) -> PublicationStore<WakingMemoryStore> {
        let max_size = memory_settings.max_size();
        Self::new(WakingMemoryStore::new(max_size), batch_size)
    }
}

impl PublicationStore<RingBuffer> {
    /// `RingBuffer` requires a file path in order to function.
    /// To provide a safe isolated file for `RingBuffer`, a
    /// `device_id` and `suffix` (local/remote) are appended to the path.
    /// This way neither pump can interfere with the other's file.
    /// It also allows for a device to have clear isolated dir for
    /// storage files.
    pub fn new_ring_buffer(
        batch_size: NonZeroUsize,
        ring_buffer_settings: &RingBufferSettings,
        device_id: String,
        suffix: &str,
    ) -> PersistResult<Self> {
        let mut file_path = ring_buffer_settings.directory().clone();
        file_path.push(device_id);
        file_path.push(suffix);
        let max_file_size = ring_buffer_settings.max_file_size();
        let flush_options = ring_buffer_settings.flush_options();
        let rb = RingBuffer::new(&file_path, max_file_size, *flush_options)?;
        Ok(Self::new(rb, batch_size))
    }
}

impl PublicationStore<RingBuffer> {
    pub fn new_ring_buffer(
        file_path: PathBuf,
        max_file_size: usize,
        flush_options: FlushOptions,
        batch_size: usize,
    ) -> StorageResult<Self> {
        let rb = RingBuffer::new(file_path, max_file_size, flush_options)?;
        Ok(Self::new(rb, batch_size))
    }
}

impl<S> PublicationStore<S>
where
    S: StreamWakeableState,
{
    pub fn new(state: S, batch_size: NonZeroUsize) -> Self {
        let state = Arc::new(Mutex::new(state));
        let loader = MessageLoader::new(state.clone(), batch_size.get());

        let inner = PublicationStoreInner { state, loader };
        let inner = Arc::new(Mutex::new(inner));

        Self(inner)
    }

    pub fn push(&self, message: &Publication) -> PersistResult<Key> {
        let inner_borrow = self.0.lock();

        let mut borrowed_store = inner_borrow.state.lock();
        let key = borrowed_store.insert(message)?;

        debug!(
            "persisted publication on topic {} with key {:?}",
            message.topic_name, key
        );

        Ok(key)
    }

    pub fn remove(&self, key: Key) -> PersistResult<()> {
        debug!("removing publication with key {:?}", key);
        let lock = self.0.lock();
        let mut state = lock.state.lock();
        state.remove(key)
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

    use std::num::NonZeroUsize;

    use bytes::Bytes;
    use futures_util::stream::TryStreamExt;
    use matches::assert_matches;
    use mqtt3::proto::{Publication, QoS};
    use test_case::test_case;

    use crate::persist::{
        publication_store::PublicationStore,
        waking_state::{
            memory::test::TestWakingMemoryStore, ring_buffer::test::TestRingBuffer,
            StreamWakeableState,
        },
        Key,
    };

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    #[tokio::test]
    async fn insert(state: impl StreamWakeableState) {
        // setup state
        let batch_size = NonZeroUsize::new(5).unwrap();
        let persistence = PublicationStore::new(state, batch_size);

        // setup data
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
        let key1 = persistence.push(&pub1).unwrap();
        let key2 = persistence.push(&pub2).unwrap();

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

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    #[tokio::test]
    async fn remove(state: impl StreamWakeableState) {
        // setup state
        let batch_size = NonZeroUsize::new(1).unwrap();
        let persistence = PublicationStore::new(state, batch_size);

        // setup data
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
        persistence.push(&pub1).unwrap();

        // get loader
        let mut loader = persistence.loader();

        // process first message, forcing loader to get new batch on the next read
        let (key1, _) = loader.try_next().await.unwrap().unwrap();
        assert_matches!(persistence.remove(key1), Ok(_));

        // add a second message and verify this is returned by loader
        let key2 = persistence.push(&pub2).unwrap();
        let extracted = loader.try_next().await.unwrap().unwrap();
        assert_eq!((extracted.0, extracted.1), (key2, pub2));
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    fn remove_key_inserted_but_not_retrieved(state: impl StreamWakeableState) {
        // setup state
        let batch_size = NonZeroUsize::new(1).unwrap();
        let persistence = PublicationStore::new(state, batch_size);

        // setup data
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // can't remove an element that hasn't been seen
        let key1 = persistence.push(&pub1).unwrap();
        let removed = persistence.remove(key1);
        assert_matches!(removed, Err(_));
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    fn remove_key_dne(state: impl StreamWakeableState) {
        // setup state
        let batch_size = NonZeroUsize::new(1).unwrap();
        let persistence = PublicationStore::new(state, batch_size);

        // setup data
        let key1 = Key { offset: 0 };

        // verify failed removal
        let removal = persistence.remove(key1);
        assert_matches!(removal, Err(_));
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    #[tokio::test]
    async fn get_loader(state: impl StreamWakeableState) {
        // setup state
        let batch_size = NonZeroUsize::new(2).unwrap();
        let persistence = PublicationStore::new(state, batch_size);

        // setup data
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
        let key1 = persistence.push(&pub1).unwrap();
        let key2 = persistence.push(&pub2).unwrap();

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
