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

use crate::persist::{waking_state::StreamWakeableState, Key, PersistError, PersistResult};

/// Message loader used to extract elements from bridge persistence
///
/// This component is responsible for message extraction from the persistence
/// It works by grabbing a snapshot of the most important messages from the persistence
/// Then, will return these elements in order
///
/// When the batch is exhausted it will grab a new batch
pub struct MessageLoader<S> {
    state: Arc<Mutex<S>>,
    batch: VecDeque<(Key, Publication)>,
    batch_size: usize,
    loaded: VecDeque<Key>,
}

impl<S> MessageLoader<S>
where
    S: StreamWakeableState,
{
    pub fn new(state: Arc<Mutex<S>>, batch_size: NonZeroUsize) -> Self {
        Self {
            state,
            batch: VecDeque::new(),
            batch_size: batch_size.get(),
            loaded: VecDeque::new(),
        }
    }

    fn next_batch(&mut self) -> PersistResult<VecDeque<(Key, Publication)>> {
        let batch = self.state.lock().batch(self.batch_size)?;
        Ok(batch)
    }
}

impl<S> Stream for MessageLoader<S>
where
    S: StreamWakeableState,
{
    type Item = PersistResult<(Key, Publication)>;

    fn poll_next(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        // return element if available
        if let Some(item) = self.batch.pop_front() {
            Poll::Ready(Some(Ok(item)))
        } else {
            // refresh next batch
            // if error, either someone forged the database or we have a database schema change
            let mut new_batch = self.next_batch()?;

            // drop those loaded keys which do not exist in the new batch
            if let Some((new_key, _)) = new_batch.front() {
                while self.loaded.front().map_or(false, |key| new_key != key) {
                    self.loaded.pop_front();
                }
            }

            // drop those items from a new batch that were loaded but not removed from storage
            if !new_batch.is_empty() {
                for key in &self.loaded {
                    match new_batch.front() {
                        Some((new_key, _)) if new_key == key => {
                            new_batch.pop_front();
                        }
                        _ => {
                            return Poll::Ready(Some(Err(PersistError::Loader {
                                key: *key,
                                loaded: self.loaded.iter().copied().collect(),
                                new_batch: new_batch.iter().map(|(key, _)| *key).collect(),
                            })));
                        }
                    }
                }
            }

            // add the tail of a new batch to items to loaded items
            self.loaded.extend(new_batch.iter().map(|(key, _)| *key));
            self.batch = new_batch;

            // get next element and return it
            self.batch.pop_front().map_or_else(
                || {
                    self.state.lock().set_waker(cx.waker());
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
    use futures_util::{future, stream::TryStreamExt};
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
    fn smaller_batch_size_respected(state: impl StreamWakeableState) {
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
        let mut loader = MessageLoader::new(state, NonZeroUsize::new(1).unwrap());
        let mut elements = loader.next_batch().unwrap();

        // verify
        assert_eq!(elements.len(), 1);
        let extracted = elements.pop_front().unwrap();
        assert_eq!((extracted.0, extracted.1), (key1, pub1));
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    fn larger_batch_size_respected(state: impl StreamWakeableState) {
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
        let mut loader = MessageLoader::new(state, NonZeroUsize::new(BATCH_SIZE).unwrap());
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
    fn ordering_maintained_across_inserts(state: impl StreamWakeableState) {
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
            assert_eq!(elements.pop_front().unwrap().0.offset, key.offset);
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
        let mut loader = MessageLoader::new(state.clone(), NonZeroUsize::new(BATCH_SIZE).unwrap());

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
    async fn retrieve_elements_beyond_batch_size(state: impl StreamWakeableState + Send + 'static) {
        // setup state
        let state = Arc::new(Mutex::new(state));
        let mut keys = vec![];
        for i in 0..=BATCH_SIZE {
            let pub1 = Publication {
                topic_name: i.to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };
            let key = state.lock().insert(&pub1).unwrap();
            keys.push(key);
        }

        let mut loader = MessageLoader::new(state.clone(), NonZeroUsize::new(BATCH_SIZE).unwrap());

        for expected_key in keys.iter().take(BATCH_SIZE) {
            let (key, _) = loader.try_next().await.unwrap().unwrap();
            assert_eq!(&key, expected_key);
        }

        // schedule to remove a publication by a key when we awaiting the last item
        let key1 = keys[0];
        tokio::spawn(async move {
            time::sleep(Duration::from_millis(10)).await;
            let key = state.lock().pop().unwrap();
            assert_eq!(key, key1);
        });

        // await the last item to be available only after the very first item is removed
        let (last_key, _) = loader.try_next().await.unwrap().unwrap();
        assert_eq!(&last_key, keys.last().unwrap());

        time::timeout(Duration::from_millis(10), loader.try_next())
            .await
            .expect_err("MessageLoader should still polling the storage");
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    #[tokio::test]
    async fn delete_and_retrieve_new_elements(state: impl StreamWakeableState) {
        // setup state
        let state = Arc::new(Mutex::new(state));

        // insert some elements
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let key1 = state.lock().insert(&pub1).unwrap();

        let pub2 = Publication {
            topic_name: "2".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let key2 = state.lock().insert(&pub2).unwrap();

        // get loader
        let mut loader = MessageLoader::new(state.clone(), NonZeroUsize::new(BATCH_SIZE).unwrap());

        // process inserted messages
        loader.try_next().await.unwrap().unwrap();
        loader.try_next().await.unwrap().unwrap();

        // remove inserted elements
        let key = state.lock().pop().unwrap();
        assert_eq!(key, key1);

        let key = state.lock().pop().unwrap();
        assert_eq!(key, key2);

        // insert new elements
        let pub3 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let key3 = state.lock().insert(&pub3).unwrap();

        // verify new elements are there
        let (key, publication) = loader.try_next().await.unwrap().unwrap();
        assert_eq!((key, publication), (key3, pub3));
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
        let mut loader = MessageLoader::new(state.clone(), NonZeroUsize::new(BATCH_SIZE).unwrap());

        // async function that waits for a message to enter the state
        let pub_copy = pub1.clone();
        let poll_stream = async move {
            let extracted = loader.try_next().await.unwrap().unwrap();
            assert_eq!((Key { offset: 0 }, pub_copy), extracted);
        };

        // add an element to the state
        let insert = async move {
            // wait to make sure that stream is polled initially
            time::sleep(Duration::from_millis(100)).await;

            // insert element once stream is polled
            {
                let mut borrowed_state = state.lock();
                let _key = borrowed_state.insert(&pub1).unwrap();
            }
        };

        future::join(poll_stream, insert).await;
    }
}
