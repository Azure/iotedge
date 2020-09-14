use std::{pin::Pin, sync::Arc, task::Context, task::Poll, vec::IntoIter};

use futures_util::stream::Stream;
use mqtt3::proto::Publication;
use parking_lot::{Mutex, MutexGuard};

use crate::persist::waking_state::StreamWakeableState;
use crate::persist::Key;

/// Message loader used to extract elements from bridge persistence
///
/// This component is responsible for message extraction from the persistence
/// It works by grabbing a snapshot of the most important messages from the persistence
/// Then, will return these elements in order
///
/// When the batch is exhausted it will grab a new batch
pub struct MessageLoader<S: StreamWakeableState> {
    state: Arc<Mutex<S>>,
    batch: IntoIter<(Key, Publication)>,
    batch_size: usize,
}

impl<S: StreamWakeableState> MessageLoader<S> {
    pub async fn new(state: Arc<Mutex<S>>, batch_size: usize) -> Self {
        let batch: IntoIter<(Key, Publication)> = Vec::new().into_iter();

        MessageLoader {
            state: Arc::clone(&state),
            batch,
            batch_size,
        }
    }
}

impl<S: StreamWakeableState> Stream for MessageLoader<S> {
    type Item = (Key, Publication);

    fn poll_next(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        if let Some(item) = self.batch.next() {
            return Poll::Ready(Some((item.0.clone(), item.1)));
        }

        let mut_self = self.get_mut();
        let mut state_lock = mut_self.state.lock();

        mut_self.batch = get_elements(&mut state_lock, mut_self.batch_size);
        mut_self.batch.next().map_or_else(
            || {
                state_lock.set_waker(cx.waker());
                Poll::Pending
            },
            |item| Poll::Ready(Some((item.0.clone(), item.1))),
        )
    }
}

fn get_elements(
    state: &mut MutexGuard<'_, impl StreamWakeableState>,
    batch_size: usize,
) -> IntoIter<(Key, Publication)> {
    let batch: Vec<_> = state
        .get(batch_size)
        .iter()
        .map(|element| (element.0.clone(), element.1.clone()))
        .collect();

    batch.into_iter()
}

#[cfg(test)]
mod tests {
    use std::marker::Send;
    use std::{iter::Iterator, sync::Arc, time::Duration};

    use bytes::Bytes;
    use futures_util::stream::StreamExt;
    use mqtt3::proto::{Publication, QoS};
    use parking_lot::Mutex;
    use tokio::{self, time};

    use crate::persist::loader::{get_elements, Key, MessageLoader};
    use crate::persist::waking_state::waking_map::WakingMap;
    use crate::persist::waking_state::StreamWakeableState;

    #[tokio::test]
    async fn smaller_batch_size_respected() {
        // setup state
        let state = WakingMap::new();
        let state = Arc::new(Mutex::new(state));

        // setup data
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let key2 = Key { offset: 1 };
        let pub2 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert elements
        let mut state_lock = state.lock();
        state_lock.insert(key1.clone(), pub1.clone()).unwrap();
        state_lock.insert(key2, pub2).unwrap();

        // get batch size elements
        let batch_size = 1;
        let iter = get_elements(&mut state_lock, batch_size);

        // verify
        let elements: Vec<_> = iter.collect();
        let extracted = elements.get(0).unwrap();
        assert_eq!(elements.len(), 1);
        assert_eq!((extracted.0.clone(), extracted.1.clone()), (key1, pub1));
    }

    #[tokio::test]
    async fn larger_batch_size_respected() {
        // setup state
        let state = WakingMap::new();
        let state = Arc::new(Mutex::new(state));

        // setup data
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let key2 = Key { offset: 1 };
        let pub2 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert elements
        let mut state_lock = state.lock();
        state_lock.insert(key1.clone(), pub1.clone()).unwrap();
        state_lock.insert(key2.clone(), pub2.clone()).unwrap();

        // get batch size elements
        let batch_size = 5;
        let elements: Vec<_> = get_elements(&mut state_lock, batch_size).collect();

        // verify
        let extracted1 = elements.get(0).unwrap();
        let extracted2 = elements.get(1).unwrap();
        assert_eq!(elements.len(), 2);
        assert_eq!((extracted1.0.clone(), extracted1.1.clone()), (key1, pub1));
        assert_eq!((extracted2.0.clone(), extracted2.1.clone()), (key2, pub2));
    }

    #[tokio::test]
    async fn retrieve_elements() {
        // setup state
        let state = WakingMap::new();
        let state = Arc::new(Mutex::new(state));

        // setup data
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let key2 = Key { offset: 1 };
        let pub2 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert some elements
        let mut state_lock = state.lock();
        state_lock.insert(key1.clone(), pub1.clone()).unwrap();
        state_lock.insert(key2.clone(), pub2.clone()).unwrap();
        drop(state_lock);

        // get loader
        let batch_size = 5;
        let mut loader = MessageLoader::new(Arc::clone(&state), batch_size).await;

        // make sure same publications come out in correct order
        let extracted1 = loader.next().await.unwrap();
        let extracted2 = loader.next().await.unwrap();
        assert_eq!(extracted1.0, key1);
        assert_eq!(extracted2.0, key2);
        assert_eq!(extracted1.1, pub1);
        assert_eq!(extracted2.1, pub2);
    }

    #[tokio::test]
    async fn delete_and_retrieve_new_elements() {
        // setup state
        let state = WakingMap::new();
        let state = Arc::new(Mutex::new(state));

        // setup data
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let key2 = Key { offset: 1 };
        let pub2 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert some elements
        let mut state_lock = state.lock();
        state_lock.insert(key1.clone(), pub1.clone()).unwrap();
        state_lock.insert(key2.clone(), pub2.clone()).unwrap();
        drop(state_lock);

        // get loader
        let batch_size = 5;
        let mut loader = MessageLoader::new(Arc::clone(&state), batch_size).await;

        // process inserted messages
        loader.next().await.unwrap();
        loader.next().await.unwrap();

        // remove inserted elements
        let mut state_lock = state.lock();
        state_lock.remove_in_flight(&key1).unwrap();
        state_lock.remove_in_flight(&key2).unwrap();
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
        state_lock.insert(key3.clone(), pub3.clone()).unwrap();
        drop(state_lock);

        // verify new elements are there
        let extracted = loader.next().await.unwrap();
        assert_eq!(extracted.0, key3);
        assert_eq!(extracted.1, pub3);
    }

    #[tokio::test]
    async fn ordering_maintained_across_inserts() {
        // setup state
        let state = WakingMap::new();
        let state = Arc::new(Mutex::new(state));

        // add many elements
        let mut state_lock = state.lock();
        let num_elements = 50 as usize;
        for i in 0..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let key = Key { offset: i as u32 };
            let publication = Publication {
                topic_name: "test".to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            state_lock.insert(key, publication).unwrap();
        }

        // verify insertion order
        let elements: Vec<_> = get_elements(&mut state_lock, num_elements).collect();
        for count in 0..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let num_elements = count as u32;

            assert_eq!(elements.get(count).unwrap().0.offset, num_elements)
        }
    }

    #[tokio::test]
    async fn poll_stream_does_not_block_when_map_empty() {
        // setup state
        let state = WakingMap::new();
        let state = Arc::new(Mutex::new(state));

        // setup data
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // get loader
        let batch_size = 5;
        let mut loader = MessageLoader::new(Arc::clone(&state), batch_size).await;

        // async function that waits for a message to enter the state
        let key_copy = key1.clone();
        let pub_copy = pub1.clone();
        let poll_stream = async move {
            let maybe_extracted = loader.next().await;
            if let Some(extracted) = maybe_extracted {
                assert_eq!((key_copy, pub_copy), extracted);
            }
        };

        // start the function and make sure it starts polling the stream before next step
        let poll_stream_handle = tokio::spawn(poll_stream);
        time::delay_for(Duration::from_secs(2)).await;

        // add an element to the state
        let mut state_lock = state.lock();
        state_lock.insert(key1, pub1).unwrap();
        drop(state_lock);
        poll_stream_handle.await.unwrap();
    }
}
