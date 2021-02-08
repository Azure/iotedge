use std::{
    collections::VecDeque,
    num::NonZeroUsize,
    pin::Pin,
    sync::Arc,
    task::{Context, Poll},
};

use futures_util::stream::Stream;
use mqtt3::proto::Publication;
use parking_lot::Mutex;

use crate::persist::{Key, StorageError, waking_state::StreamWakeableState};

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
    pub fn new(state: Arc<Mutex<S>>, batch_size: NonZeroUsize) -> Self {
        let batch = VecDeque::new();

        let inner = MessageLoaderInner {
            state,
            batch,
            batch_size: batch_size.get(),
        };
        let inner = Arc::new(Mutex::new(inner));

        Self(inner)
    }

    fn next_batch(&mut self) -> Result<VecDeque<(Key, Publication)>, StorageError> {
        let inner = self.0.lock();
        let state = inner.state.clone();
        let mut borrowed_state = state.lock();
        borrowed_state.batch(inner.batch_size)
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
    type Item = Result<(Key, Publication), StorageError>;

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
    use std::{num::NonZeroUsize, sync::Arc, time::Duration};

    use bytes::Bytes;
    use futures_util::{future::join, stream::TryStreamExt};
    use mqtt3::proto::{Publication, QoS};
    use parking_lot::Mutex;
    use test_case::test_case;
    use tokio::time;

    use crate::persist::{
        loader::{Key, MessageLoader},
        waking_state::{memory::test::TestWakingMemoryStore, ring_buffer::test::TestRingBuffer},
        StreamWakeableState,
    };

    const BATCH_SIZE: usize = 5;

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    #[tokio::test]
    async fn smaller_batch_size_respected(state: impl StreamWakeableState) {
        // setup state
        let state = Arc::new(Mutex::new(state));

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

        // insert elements
        let key1;
        {
            let mut borrowed_state = state.lock();
            key1 = borrowed_state.insert(&pub1).unwrap();
            let _key2 = borrowed_state.insert(&pub2).unwrap();
        }
        // get batch size elements
        let mut loader = MessageLoader::new(state, 1);
        let mut elements = loader.next_batch().unwrap();

        // verify
        assert_eq!(elements.len(), 1);
        let extracted = elements.pop_front().unwrap();
        assert_eq!((extracted.0, extracted.1), (key1, pub1));
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    #[tokio::test]
    async fn larger_batch_size_respected(state: impl StreamWakeableState) {
        // setup state
        let state = Arc::new(Mutex::new(state));

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

        // insert elements
        let key1;
        let key2;
        {
            let mut borrowed_state = state.lock();
            key1 = borrowed_state.insert(&pub1).unwrap();
            key2 = borrowed_state.insert(&pub2).unwrap();
        }

        // get batch size elements
        let mut loader = MessageLoader::new(state, BATCH_SIZE);
        let mut elements = loader.next_batch().unwrap();

        // verify
        assert_eq!(elements.len(), 2);
        let extracted1 = elements.pop_front().unwrap();
        let extracted2 = elements.pop_front().unwrap();
        assert_eq!((extracted1.0, extracted1.1), (key1, pub1));
        assert_eq!((extracted2.0, extracted2.1), (key2, pub2));
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    #[tokio::test]
    async fn ordering_maintained_across_inserts(state: impl StreamWakeableState) {
        // setup state
        let state = Arc::new(Mutex::new(state));

        // add many elements
        let num_elements = 10_usize;
        let mut keys = vec![];
        for i in 0..num_elements {
            let publication = Publication {
                topic_name: i.to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            let key = {
                let mut borrowed_state = state.lock();
                borrowed_state.insert(&publication).unwrap()
            };
            keys.push(key);
        }

        // verify insertion order
        let mut loader = MessageLoader::new(state, NonZeroUsize::new(num_elements).unwrap());
        let mut elements = loader.next_batch().unwrap();

        for key in keys {
            assert_eq!(elements.pop_front().unwrap().0.offset, key.offset)
        }
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    #[tokio::test]
    async fn retrieve_elements(state: impl StreamWakeableState) {
        // setup state
        let state = Arc::new(Mutex::new(state));

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
        let key1;
        let key2;
        {
            let mut borrowed_state = state.lock();
            key1 = borrowed_state.insert(&pub1).unwrap();
            key2 = borrowed_state.insert(&pub2).unwrap();
        }
        // get loader
        let mut loader = MessageLoader::new(state.clone(), BATCH_SIZE);

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
    async fn delete_and_retrieve_new_elements(state: impl StreamWakeableState) {
        // setup state
        let state = Arc::new(Mutex::new(state));

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
        let key1;
        let key2;
        {
            let mut borrowed_state = state.lock();
            key1 = borrowed_state.insert(&pub1).unwrap();
            key2 = borrowed_state.insert(&pub2).unwrap();
        }

        // get loader
        let mut loader = MessageLoader::new(state.clone(), BATCH_SIZE);

        // process inserted messages
        loader.try_next().await.unwrap().unwrap();
        loader.try_next().await.unwrap().unwrap();

        // remove inserted elements
        {
            let mut borrowed_state = state.lock();
            borrowed_state.remove(key1).unwrap();
            borrowed_state.remove(key2).unwrap();
        }

        // insert new elements
        let pub3 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let key3;
        {
            let mut borrowed_state = state.lock();
            key3 = borrowed_state.insert(&pub3).unwrap();
        }
        // verify new elements are there
        let extracted = loader.try_next().await.unwrap().unwrap();
        assert_eq!(extracted.0, key3);
        assert_eq!(extracted.1, pub3);
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    #[tokio::test]
    async fn poll_stream_does_not_block_when_map_empty(state: impl StreamWakeableState) {
        // setup state
        let state = Arc::new(Mutex::new(state));

        // setup data
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // get loader
        let mut loader = MessageLoader::new(state.clone(), BATCH_SIZE);

        // async function that waits for a message to enter the state
        let pub_copy = pub1.clone();
        let poll_stream = async move {
            let extracted = loader.try_next().await.unwrap().unwrap();
            assert_eq!((Key { offset: 0 }, pub_copy), extracted);
        };

        // add an element to the state
        let insert = async move {
            // wait to make sure that stream is polled initially
            time::delay_for(Duration::from_secs(2)).await;

            // insert element once stream is polled
            {
                let mut borrowed_state = state.lock();
                let _key = borrowed_state.insert(&pub1).unwrap();
            }
        };

        join(poll_stream, insert).await;
    }
}
