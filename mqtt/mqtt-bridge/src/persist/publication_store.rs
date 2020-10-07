#![allow(dead_code)] // TODO remove when ready
use std::{cell::RefCell, rc::Rc};

use anyhow::Result;
use mqtt3::proto::Publication;
use tracing::debug;

use crate::persist::{
    loader::MessageLoader, waking_state::StreamWakeableState, Key, PersistError, WakingMemoryStore,
};

/// Persistence implementation used for the bridge
pub struct PublicationStore<S: StreamWakeableState> {
    state: Rc<RefCell<S>>,
    offset: u32,
    loader: Rc<RefCell<MessageLoader<S>>>,
}

impl PublicationStore<WakingMemoryStore> {
    pub fn new_memory(batch_size: usize) -> PublicationStore<WakingMemoryStore> {
        Self::new(WakingMemoryStore::new(), batch_size)
    }
}

impl<S: StreamWakeableState> PublicationStore<S> {
    pub fn new(state: S, batch_size: usize) -> Self {
        let state = Rc::new(RefCell::new(state));
        let loader = MessageLoader::new(state.clone(), batch_size);
        let loader = Rc::new(RefCell::new(loader));

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

        let mut state_borrow = self
            .state
            .try_borrow_mut()
            .map_err(PersistError::BorrowSharedState)?;
        state_borrow.insert(key, message)?;
        self.offset += 1;
        Ok(key)
    }

    pub fn remove(&mut self, key: Key) -> Result<(), PersistError> {
        debug!("removing publication with key {:?}", key);

        let mut state_borrow = self
            .state
            .try_borrow_mut()
            .map_err(PersistError::BorrowSharedState)?;
        state_borrow.remove(key)?;
        Ok(())
    }

    pub fn loader(&mut self) -> Rc<RefCell<MessageLoader<S>>> {
        self.loader.clone()
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
        let mut loader_borrow = loader.borrow_mut();

        // make sure same publications come out in correct order
        let extracted1 = loader_borrow.try_next().await.unwrap().unwrap();
        let extracted2 = loader_borrow.try_next().await.unwrap().unwrap();
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
        let mut loader_borrow = loader.borrow_mut();

        // process first message, forcing loader to get new batch on the next read
        let (key1, _) = loader_borrow.try_next().await.unwrap().unwrap();
        assert_matches!(persistence.remove(key1), Ok(_));

        // add a second message and verify this is returned by loader
        persistence.push(pub2.clone()).unwrap();
        let extracted = loader_borrow.try_next().await.unwrap().unwrap();
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
        let mut loader_borrow = loader.borrow_mut();

        // verify the loader returns both elements
        let extracted1 = loader_borrow.try_next().await.unwrap().unwrap();
        let extracted2 = loader_borrow.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.0, key1);
        assert_eq!(extracted2.0, key2);
        assert_eq!(extracted1.1, pub1);
        assert_eq!(extracted2.1, pub2);
    }
}
