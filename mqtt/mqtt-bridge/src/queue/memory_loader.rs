use std::cell::RefCell;
use std::cmp::min;
use std::collections::btree_map::Range;
use std::collections::BTreeMap;
use std::iter::Take;
use std::pin::Pin;
use std::rc::Rc;
use std::slice::Iter;
use std::sync::Arc;
use std::task::Context;
use std::{task::Poll, vec::IntoIter};

use anyhow::Result;
use futures_util::stream::Stream;
use mqtt3::proto::Publication;
use tokio::sync::Mutex;
use tokio::sync::MutexGuard;

use crate::queue::{waking_map::WakingMap, Key, QueueError};

// TODO: should this have some way of shutting down? Callers reading stream will hang?
pub struct InMemoryMessageLoader {
    state: Arc<Mutex<WakingMap>>,
    batch: IntoIter<(Key, Publication)>,
    batch_size: usize,
}

impl InMemoryMessageLoader {
    pub async fn new(state: Arc<Mutex<WakingMap>>, batch_size: usize) -> Self {
        let state_lock = state.lock().await;
        let batch = get_elements(&state_lock, batch_size);

        InMemoryMessageLoader {
            state: Arc::clone(&state),
            batch,
            batch_size,
        }
    }
}

impl Stream for InMemoryMessageLoader {
    type Item = (Key, Publication);

    fn poll_next(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        if let Some(item) = self.batch.next() {
            return Poll::Ready(Some((item.0.clone(), item.1.clone())));
        }

        let mut state_lock;
        let mut_self = self.get_mut();
        loop {
            if let Ok(lock) = mut_self.state.try_lock() {
                state_lock = lock;
                break;
            }
        }

        mut_self.batch = get_elements(&state_lock, mut_self.batch_size);
        mut_self.batch.next().map_or_else(
            || {
                state_lock.set_waker(cx.waker());
                Poll::Pending
            },
            |item| Poll::Ready(Some((item.0.clone(), item.1.clone()))),
        )
    }
}

fn get_elements(state: &MutexGuard<WakingMap>, batch_size: usize) -> IntoIter<(Key, Publication)> {
    let batch: Vec<_> = state
        .get_map()
        .iter()
        .take(batch_size)
        .map(|element| (element.0.clone(), element.1.clone()))
        .collect();
    batch.into_iter()
}

#[cfg(test)]
mod tests {
    use std::cell::RefCell;
    use std::collections::BTreeMap;
    use std::iter::Iterator;
    use std::rc::Rc;
    use std::sync::Arc;
    use std::time::Duration;

    use async_std::task;
    use bytes::Bytes;
    use futures_util::stream::Stream;
    use futures_util::stream::StreamExt;
    use mqtt3::proto::{Publication, QoS};
    use tokio;
    use tokio::sync::Mutex;
    use tokio::time;

    use crate::queue::simple_message_loader::InMemoryMessageLoader;
    use crate::queue::{
        simple_message_loader::get_elements, waking_map::WakingMap, Key, QueueError,
    };

    #[tokio::test]
    async fn smaller_batch_size_respected() {
        // setup state
        let state = BTreeMap::new();
        let state = WakingMap::new(state);
        let state = Arc::new(Mutex::new(state));

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
        let key2 = Key {
            priority: 0,
            offset: 1,
            ttl: Duration::from_secs(5),
        };
        let pub2 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert elements
        let mut state_lock = state.lock().await;
        state_lock.insert(key1.clone(), pub1.clone());
        state_lock.insert(key2.clone(), pub2.clone());

        // get batch size elements
        let batch_size = 1;
        let iter = get_elements(&state_lock, batch_size);

        // verify
        let elements: Vec<_> = iter.collect();
        let extracted = elements.get(0).unwrap();
        assert_eq!(elements.len(), 1);
        assert_eq!((extracted.0.clone(), extracted.1.clone()), (key1, pub1));
    }

    #[tokio::test]
    async fn larger_batch_size_respected() {
        // setup state
        let state = BTreeMap::new();
        let state = WakingMap::new(state);
        let state = Arc::new(Mutex::new(state));

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
        let key2 = Key {
            priority: 0,
            offset: 1,
            ttl: Duration::from_secs(5),
        };
        let pub2 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert elements
        let mut state_lock = state.lock().await;
        state_lock.insert(key1.clone(), pub1.clone());
        state_lock.insert(key2.clone(), pub2.clone());

        // get batch size elements
        let batch_size = 5;
        let elements: Vec<_> = get_elements(&state_lock, batch_size).collect();

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
        let state = BTreeMap::new();
        let state = WakingMap::new(state);
        let state = Arc::new(Mutex::new(state));

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
        let key2 = Key {
            priority: 0,
            offset: 1,
            ttl: Duration::from_secs(5),
        };
        let pub2 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert some elements
        let mut state_lock = state.lock().await;
        state_lock.insert(key1.clone(), pub1.clone());
        state_lock.insert(key2.clone(), pub2.clone());
        drop(state_lock);

        // init loader
        let batch_size = 5;
        let mut loader = InMemoryMessageLoader::new(Arc::clone(&state), batch_size).await;

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
        let state = BTreeMap::new();
        let state = WakingMap::new(state);
        let state = Arc::new(Mutex::new(state));

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
        let key2 = Key {
            priority: 0,
            offset: 1,
            ttl: Duration::from_secs(5),
        };
        let pub2 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // insert some elements
        let mut state_lock = state.lock().await;
        state_lock.insert(key1.clone(), pub1.clone());
        state_lock.insert(key2.clone(), pub2.clone());
        drop(state_lock);

        // init loader
        let batch_size = 5;
        let mut loader = InMemoryMessageLoader::new(Arc::clone(&state), batch_size).await;

        // process inserted messages
        loader.next().await.unwrap();
        loader.next().await.unwrap();

        // remove inserted elements
        let mut state_lock = state.lock().await;
        state_lock.remove(key1.clone());
        state_lock.remove(key2.clone());
        drop(state_lock);

        // insert new elements
        let key3 = Key {
            priority: 0,
            offset: 2,
            ttl: Duration::from_secs(5),
        };
        let pub3 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };
        let mut state_lock = state.lock().await;
        state_lock.insert(key3.clone(), pub3.clone());
        drop(state_lock);

        // verify new elements are there
        let extracted = loader.next().await.unwrap();
        assert_eq!(extracted.0, key3);
        assert_eq!(extracted.1, pub3);
    }

    #[tokio::test]
    async fn ordering_maintained_across_inserts() {
        // setup state
        let state = BTreeMap::new();
        let state = WakingMap::new(state);
        let state = Arc::new(Mutex::new(state));

        // add many elements
        let mut state_lock = state.lock().await;
        let num_elements = 50 as usize;
        for i in 0..num_elements {
            let key = Key {
                priority: 0,
                offset: i as u32,
                ttl: Duration::from_secs(5),
            };
            let publication = Publication {
                topic_name: "test".to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            state_lock.insert(key, publication)
        }

        // verify insertion order
        let elements: Vec<_> = get_elements(&state_lock, num_elements).collect();
        for count in 0..num_elements {
            assert_eq!(elements.get(count).unwrap().0.offset, count as u32)
        }
    }

    #[tokio::test]
    async fn ordering_maintained_across_delete() {
        // setup state
        let state = BTreeMap::new();
        let state = WakingMap::new(state);
        let state = Arc::new(Mutex::new(state));

        // add many elements
        let mut state_lock = state.lock().await;
        let num_elements = 50 as usize;
        for i in 0..num_elements {
            let key = Key {
                priority: 0,
                offset: i as u32,
                ttl: Duration::from_secs(5),
            };
            let publication = Publication {
                topic_name: "test".to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            state_lock.insert(key, publication)
        }

        // delete an element
        let index_to_delete = 25;
        let key_to_delete = Key {
            priority: 0,
            offset: 25,
            ttl: Duration::from_secs(5),
        };
        state_lock.remove(key_to_delete);

        // verify insertion order
        let elements: Vec<_> = get_elements(&state_lock, num_elements).collect();
        assert_eq!(elements.len(), num_elements - 1);

        let mut compare_offset = 0;
        for element in elements {
            if compare_offset == index_to_delete {
                compare_offset += 1;
            }

            assert_eq!(element.0.offset, compare_offset);
            compare_offset += 1;
        }
    }

    // TODO REVIEW: replace wait with notify
    #[tokio::test]
    async fn poll_stream_does_not_block_when_map_empty() {
        // setup state
        let state = BTreeMap::new();
        let state = WakingMap::new(state);
        let state = Arc::new(Mutex::new(state));

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

        // init loader
        let batch_size = 5;
        let mut loader = InMemoryMessageLoader::new(Arc::clone(&state), batch_size).await;

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
        let mut state_lock = state.lock().await;
        state_lock.insert(key1, pub1);
        drop(state_lock);
        poll_stream_handle.await.unwrap();
    }
}
