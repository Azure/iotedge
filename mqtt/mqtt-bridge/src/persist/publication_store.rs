#![allow(dead_code)] // TODO remove when ready
use std::sync::Arc;

use anyhow::Result;
use mqtt3::proto::Publication;
use parking_lot::Mutex;
// use rocksdb::DB;
use tokio::sync::Mutex as AsyncMutex;
use tracing::debug;

// use crate::persist::{
//     loader::MessageLoader, waking_state::StreamWakeableState, Key, PersistError, WakingMemoryStore,
//     WakingRocksDBStore,
// };
use crate::persist::{
    loader::MessageLoader, waking_state::StreamWakeableState, Key, PersistError, WakingMemoryStore,
};

/// Persistence implementation used for the bridge
pub struct PublicationStore<S: StreamWakeableState> {
    state: Arc<Mutex<S>>,
    offset: u32,
    loader: Arc<AsyncMutex<MessageLoader<S>>>,
}

impl PublicationStore<WakingMemoryStore> {
    pub fn new_memory(batch_size: usize) -> PublicationStore<WakingMemoryStore> {
        Self::new(WakingMemoryStore::new(), batch_size)
    }
}

// impl PublicationStore<WakingRocksDBStore> {
//     pub fn new_disk(
//         db: DB,
//         column_family: String,
//         batch_size: usize,
//     ) -> Result<PublicationStore<WakingRocksDBStore>, PersistError> {
//         let waking_store = WakingRocksDBStore::new(db, column_family)?;
//         Ok(Self::new(waking_store, batch_size))
//     }
// }

impl<S: StreamWakeableState> PublicationStore<S> {
    pub fn new(state: S, batch_size: usize) -> Self {
        let state = Arc::new(Mutex::new(state));
        let loader = MessageLoader::new(Arc::clone(&state), batch_size);
        let loader = Arc::new(AsyncMutex::new(loader));

        let offset = 0;
        Self {
            state,
            offset,
            loader,
        }
    }

    pub fn push(&mut self, message: Publication) -> Result<Key, PersistError> {
        debug!(
            "persisting publication on topic {} with offset {}",
            message.topic_name, self.offset
        );

        let key = Key {
            offset: self.offset,
        };

        let mut state_lock = self.state.lock();
        state_lock.insert(key, message)?;
        self.offset += 1;
        Ok(key)
    }

    pub fn remove(&mut self, key: Key) -> Result<(), PersistError> {
        debug!("removing publication with offset {}", self.offset);

        let mut state_lock = self.state.lock();
        state_lock.remove(key)?;
        Ok(())
    }

    pub fn loader(&mut self) -> Arc<AsyncMutex<MessageLoader<S>>> {
        Arc::clone(&self.loader)
    }
}

#[cfg(test)]
mod tests {
    use bytes::Bytes;
    use futures_util::stream::TryStreamExt;
    use matches::assert_matches;
    use mqtt3::proto::{Publication, QoS};

    use crate::persist::{publication_store::PublicationStore, Key, WakingMemoryStore};

    #[tokio::test]
    async fn insert() {
        // setup state
        let state = WakingMemoryStore::new();
        let batch_size: usize = 5;
        let mut persistence = PublicationStore::new(state, batch_size);

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
        persistence.push(pub1.clone()).unwrap();
        persistence.push(pub2.clone()).unwrap();

        // get loader
        let loader = persistence.loader();
        let mut loader = loader.lock().await;

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
        let state = WakingMemoryStore::new();
        let batch_size: usize = 1;
        let mut persistence = PublicationStore::new(state, batch_size);

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
        persistence.push(pub1.clone()).unwrap();

        // get loader
        let loader = persistence.loader();
        let mut loader = loader.lock().await;

        // process first message, forcing loader to get new batch on the next read
        loader.try_next().await.unwrap().unwrap();
        assert_matches!(persistence.remove(key1), Ok(_));

        // add a second message and verify this is returned by loader
        persistence.push(pub2.clone()).unwrap();
        let extracted = loader.try_next().await.unwrap().unwrap();
        assert_eq!((extracted.0, extracted.1), (key2, pub2));
    }

    #[tokio::test]
    async fn remove_key_inserted_but_not_retrieved() {
        // setup state
        let state = WakingMemoryStore::new();
        let batch_size: usize = 1;
        let mut persistence = PublicationStore::new(state, batch_size);

        // setup data
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // can't remove an element that hasn't been seen
        persistence.push(pub1).unwrap();
        let removed = persistence.remove(key1);
        assert_matches!(removed, Err(_));
    }

    #[tokio::test]
    async fn remove_key_dne() {
        // setup state
        let state = WakingMemoryStore::new();
        let batch_size: usize = 1;
        let mut persistence = PublicationStore::new(state, batch_size);

        // setup data
        let key1 = Key { offset: 0 };

        // verify failed removal
        let removal = persistence.remove(key1);
        assert_matches!(removal, Err(_));
    }

    #[tokio::test]
    async fn get_loader() {
        // setup state
        let state = WakingMemoryStore::new();
        let batch_size: usize = 1;
        let mut persistence = PublicationStore::new(state, batch_size);

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
        persistence.push(pub1.clone()).unwrap();
        persistence.push(pub2.clone()).unwrap();

        // get loader with batch size
        let loader = persistence.loader();
        let mut loader = loader.lock().await;

        // verify the loader returns both elements
        let extracted1 = loader.try_next().await.unwrap().unwrap();
        let extracted2 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.0, key1);
        assert_eq!(extracted2.0, key2);
        assert_eq!(extracted1.1, pub1);
        assert_eq!(extracted2.1, pub2);
    }
}
