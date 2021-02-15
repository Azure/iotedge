use std::{
    collections::VecDeque,
    pin::Pin,
    sync::Arc,
    task::{Context, Poll},
};

use futures_util::stream::Stream;
use mqtt3::proto::Publication;
use parking_lot::Mutex;

use crate::persist::{waking_state::StreamWakeableState, Key, PersistError};

/// Pattern allows for the wrapping `MessageLoader` to be cloned and have non mutable methods
/// This facilitates sharing between multiple futures in a single threaded environment
pub struct MessageLoaderInner<S> {
    state: Arc<Mutex<S>>,
    batch: VecDeque<(Key, Publication)>,
    batch_size: usize,
}

/// Message loader used to extract elements from bridge persistence
///
/// This component is responsible for message extraction from the persistence
/// It works by grabbing a snapshot of the most important messages from the persistence
/// Then, will return these elements in order
///
/// When the batch is exhausted it will grab a new batch
pub struct MessageLoader<S>(Arc<Mutex<MessageLoaderInner<S>>>);

impl<S> MessageLoader<S>
where
    S: StreamWakeableState,
{
    pub fn new(state: Arc<Mutex<S>>, batch_size: usize) -> Self {
        let batch = VecDeque::new();

        let inner = MessageLoaderInner {
            state,
            batch,
            batch_size,
        };
        let inner = Arc::new(Mutex::new(inner));

        Self(inner)
    }

    fn next_batch(&mut self) -> Result<VecDeque<(Key, Publication)>, PersistError> {
        let inner = self.0.lock();
        let mut state_lock = inner.state.lock();
        let batch = state_lock.batch(inner.batch_size)?;

        Ok(batch)
    }
}

impl<S: StreamWakeableState> Clone for MessageLoader<S> {
    fn clone(&self) -> Self {
        Self(self.0.clone())
    }
}

impl<S> Stream for MessageLoader<S>
where
    S: StreamWakeableState,
{
    type Item = Result<(Key, Publication), PersistError>;

    fn poll_next(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        let mut inner = self.0.lock();

        // return element if available
        if let Some(item) = inner.batch.pop_front() {
            Poll::Ready(Some(Ok(item)))
        } else {
            drop(inner);

            // refresh next batch
            // if error, either someone forged the database or we have a database schema change
            let next_batch = self.next_batch()?;
            let mut inner = self.0.lock();
            inner.batch = next_batch;

            // get next element and return it
            let maybe_extracted = inner.batch.pop_front();
            let mut state_lock = inner.state.lock();
            maybe_extracted.map_or_else(
                || {
                    state_lock.set_waker(cx.waker());
                    Poll::Pending
                },
                |extracted| Poll::Ready(Some(Ok(extracted))),
            )
        }
    }
}

#[cfg(test)]
mod tests {
    use std::{sync::Arc, time::Duration};

    use bytes::Bytes;
    use futures_util::{future::join, stream::TryStreamExt};
    use mqtt3::proto::{Publication, QoS};
    use tokio::time;

    use crate::persist::{
        loader::{Key, MessageLoader},
        waking_state::StreamWakeableState,
        WakingMemoryStore,
    };
    use parking_lot::Mutex;

    #[test]
    fn smaller_batch_size_respected() {
        // setup state
        let state = WakingMemoryStore::default();
        let state = Arc::new(Mutex::new(state));

        // setup data
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let key2 = Key { offset: 1 };
        let pub2 = Publication {
            topic_name: "2".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert elements
        let mut state_lock = state.lock();
        state_lock.insert(key1, pub1.clone()).unwrap();
        state_lock.insert(key2, pub2).unwrap();
        drop(state_lock);

        // get batch size elements
        let batch_size = 1;
        let mut loader = MessageLoader::new(state, batch_size);
        let mut elements = loader.next_batch().unwrap();

        // verify
        assert_eq!(elements.len(), 1);
        let extracted = elements.pop_front().unwrap();
        assert_eq!((extracted.0, extracted.1), (key1, pub1));
    }

    #[test]
    fn larger_batch_size_respected() {
        // setup state
        let state = WakingMemoryStore::default();
        let state = Arc::new(Mutex::new(state));

        // setup data
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let key2 = Key { offset: 1 };
        let pub2 = Publication {
            topic_name: "2".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert elements
        let mut state_lock = state.lock();
        state_lock.insert(key1, pub1.clone()).unwrap();
        state_lock.insert(key2, pub2.clone()).unwrap();
        drop(state_lock);

        // get batch size elements
        let batch_size = 5;
        let mut loader = MessageLoader::new(state, batch_size);
        let mut elements = loader.next_batch().unwrap();

        // verify
        assert_eq!(elements.len(), 2);
        let extracted1 = elements.pop_front().unwrap();
        let extracted2 = elements.pop_front().unwrap();
        assert_eq!((extracted1.0, extracted1.1), (key1, pub1));
        assert_eq!((extracted2.0, extracted2.1), (key2, pub2));
    }

    #[test]
    fn ordering_maintained_across_inserts() {
        // setup state
        let state = WakingMemoryStore::default();
        let state = Arc::new(Mutex::new(state));

        // add many elements
        let mut state_lock = state.lock();
        let num_elements = 10_usize;
        for i in 0..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let key = Key { offset: i as u32 };
            let publication = Publication {
                topic_name: i.to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            state_lock.insert(key, publication).unwrap();
        }
        drop(state_lock);

        // verify insertion order
        let mut loader = MessageLoader::new(state, num_elements);
        let mut elements = loader.next_batch().unwrap();

        for count in 0..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let num_elements = count as u32;

            assert_eq!(elements.pop_front().unwrap().0.offset, num_elements)
        }
    }

    #[tokio::test]
    async fn retrieve_elements() {
        // setup state
        let state = WakingMemoryStore::default();
        let state = Arc::new(Mutex::new(state));

        // setup data
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let key2 = Key { offset: 1 };
        let pub2 = Publication {
            topic_name: "2".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert some elements
        let mut state_lock = state.lock();
        state_lock.insert(key1, pub1.clone()).unwrap();
        state_lock.insert(key2, pub2.clone()).unwrap();
        drop(state_lock);

        // get loader
        let batch_size = 5;
        let mut loader = MessageLoader::new(state.clone(), batch_size);

        // make sure same publications come out in correct order
        let extracted1 = loader.try_next().await.unwrap().unwrap();
        let extracted2 = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted1.0, key1);
        assert_eq!(extracted2.0, key2);
        assert_eq!(extracted1.1, pub1);
        assert_eq!(extracted2.1, pub2);
    }

    #[tokio::test]
    async fn delete_and_retrieve_new_elements() {
        // setup state
        let state = WakingMemoryStore::default();
        let state = Arc::new(Mutex::new(state));

        // setup data
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let key2 = Key { offset: 1 };
        let pub2 = Publication {
            topic_name: "2".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert some elements
        let mut state_lock = state.lock();
        state_lock.insert(key1, pub1.clone()).unwrap();
        state_lock.insert(key2, pub2.clone()).unwrap();
        drop(state_lock);

        // get loader
        let batch_size = 5;
        let mut loader = MessageLoader::new(state.clone(), batch_size);

        // process inserted messages
        loader.try_next().await.unwrap().unwrap();
        loader.try_next().await.unwrap().unwrap();

        // remove inserted elements
        let mut state_lock = state.lock();
        state_lock.remove(key1).unwrap();
        state_lock.remove(key2).unwrap();
        drop(state_lock);

        // insert new elements
        let key3 = Key { offset: 2 };
        let pub3 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let mut state_lock = state.lock();
        state_lock.insert(key3, pub3.clone()).unwrap();
        drop(state_lock);

        // verify new elements are there
        let extracted = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted.0, key3);
        assert_eq!(extracted.1, pub3);
    }

    #[tokio::test]
    async fn poll_stream_does_not_block_when_map_empty() {
        // setup state
        let state = WakingMemoryStore::default();
        let state = Arc::new(Mutex::new(state));

        // setup data
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // get loader
        let batch_size = 5;
        let mut loader = MessageLoader::new(state.clone(), batch_size);

        // async function that waits for a message to enter the state
        let key_copy = key1;
        let pub_copy = pub1.clone();
        let poll_stream = async move {
            let extracted = loader.try_next().await.unwrap().unwrap();
            assert_eq!((key_copy, pub_copy), extracted);
        };

        // add an element to the state
        let insert = async move {
            // wait to make sure that stream is polled initially
            time::delay_for(Duration::from_secs(2)).await;

            // insert element once stream is polled
            let mut state_lock = state.lock();
            state_lock.insert(key1, pub1).unwrap();
            drop(state_lock);
        };

        join(poll_stream, insert).await;
    }
}
