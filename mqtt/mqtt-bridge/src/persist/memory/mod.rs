use std::{io::Error, sync::Arc};

use anyhow::Result;
use async_trait::async_trait;
use mqtt3::proto::Publication;
use parking_lot::Mutex;
use tracing::debug;

use crate::persist::{
    memory::loader::InMemoryMessageLoader, memory::waking_map::WakingMap, Key, Persist,
};

pub mod loader;
mod waking_map;

/// In memory persistence implementation used for the bridge
pub struct InMemoryPersist {
    state: Arc<Mutex<WakingMap>>,
    offset: u32,
    loader: Arc<Mutex<InMemoryMessageLoader>>,
}

#[async_trait]
impl<'a> Persist<'a> for InMemoryPersist {
    type Loader = InMemoryMessageLoader;
    type Error = Error;

    fn new(batch_size: usize) -> Self {
        let state = WakingMap::new();
        let state = Arc::new(Mutex::new(state));

        let loader = InMemoryMessageLoader::new(&Arc::clone(&state), batch_size);
        let loader = Arc::new(Mutex::new(loader));

        let offset = 0;
        InMemoryPersist {
            state,
            offset,
            loader,
        }
    }

    async fn push(&mut self, message: Publication) -> Result<Key, Self::Error> {
        debug!(
            "persisting publication on topic {} with offset {}",
            message.topic_name, self.offset
        );

        let key = Key {
            offset: self.offset,
        };

        let mut state_lock = self.state.lock();
        state_lock.insert(key.clone(), message);
        self.offset += 1;
        Ok(key)
    }

    async fn remove(&mut self, key: Key) -> Option<Publication> {
        debug!(
            "removing publication with offset {} from in-flight collection",
            self.offset
        );

        let mut state_lock = self.state.lock();
        state_lock.remove_in_flight(&key)
    }

    async fn loader(&'a mut self) -> Arc<Mutex<InMemoryMessageLoader>> {
        Arc::clone(&self.loader)
    }
}

#[cfg(test)]
mod tests {
    use bytes::Bytes;
    use futures_util::stream::StreamExt;
    use matches::assert_matches;
    use mqtt3::proto::{Publication, QoS};

    use crate::persist::{memory::InMemoryPersist, Key, Persist};

    #[tokio::test]
    async fn insert() {
        // setup state
        let batch_size: usize = 5;
        let mut persistence = InMemoryPersist::new(batch_size);

        // setup data
        let key1 = Key { offset: 0 };
        let key2 = Key { offset: 1 };
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
        persistence.push(pub1.clone()).await.unwrap();
        persistence.push(pub2.clone()).await.unwrap();

        // get loader
        let loader = persistence.loader().await;
        let mut loader = loader.lock();

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
        let batch_size: usize = 1;
        let mut persistence = InMemoryPersist::new(batch_size);

        // setup data
        let key1 = Key { offset: 0 };
        let key2 = Key { offset: 1 };
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
        persistence.push(pub1.clone()).await.unwrap();

        // get loader
        let loader = persistence.loader().await;
        let mut loader = loader.lock();

        // process first message, forcing loader to get new batch on the next read
        loader.next().await.unwrap();
        persistence.remove(key1).await.unwrap();

        // add a second message and verify this is returned first by loader
        persistence.push(pub2.clone()).await.unwrap();
        let extracted = loader.next().await.unwrap();
        assert_eq!((extracted.0, extracted.1), (key2, pub2));
    }

    #[tokio::test]
    async fn remove_key_that_dne() {
        // setup state
        let batch_size: usize = 1;
        let mut persistence = InMemoryPersist::new(batch_size);

        // setup data
        let key1 = Key { offset: 0 };

        // verify failed removal
        let removal = persistence.remove(key1).await;
        assert_matches!(removal, None);
    }

    #[tokio::test]
    async fn get_loader() {
        // setup state
        let batch_size: usize = 1;
        let mut persistence = InMemoryPersist::new(batch_size);

        // setup data
        let key1 = Key { offset: 0 };
        let key2 = Key { offset: 1 };
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
        persistence.push(pub1.clone()).await.unwrap();
        persistence.push(pub2.clone()).await.unwrap();

        // get loader with batch size
        let loader = persistence.loader().await;
        let mut loader = loader.lock();

        // the persistence never removed any elements
        // therefore the loader should get the same element in the two batches of size 1
        let extracted1 = loader.next().await.unwrap();
        let extracted2 = loader.next().await.unwrap();
        assert_eq!(extracted1.0, key1);
        assert_eq!(extracted2.0, key2);
        assert_eq!(extracted1.1, pub1);
        assert_eq!(extracted2.1, pub2);
    }
}
