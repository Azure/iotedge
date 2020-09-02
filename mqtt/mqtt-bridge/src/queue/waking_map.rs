use std::collections::BTreeMap;
use std::task::Waker;

use mqtt3::proto::Publication;

use crate::queue::Key;
pub struct WakingMap {
    map: BTreeMap<Key, Publication>,
    waker: Option<Waker>,
}

impl WakingMap {
    pub fn new(map: BTreeMap<Key, Publication>) -> Self {
        WakingMap { map, waker: None }
    }

    pub fn insert(&mut self, key: Key, value: Publication) {
        self.map.insert(key, value);

        if let Some(waker) = self.waker.clone() {
            waker.wake();
        }
    }

    pub fn remove(&mut self, key: Key) -> Option<Publication> {
        self.map.remove(&key)
    }

    // exposed for specific loading logic
    pub fn get_map(&self) -> &BTreeMap<Key, Publication> {
        &self.map
    }

    pub fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}

#[cfg(test)]
mod tests {
    use std::collections::BTreeMap;
    use std::pin::Pin;
    use std::sync::Arc;
    use std::task::Context;
    use std::task::Poll;
    use std::time::Duration;

    use bytes::Bytes;
    use futures_util::stream::Stream;
    use futures_util::stream::StreamExt;
    use mqtt3::proto::{Publication, QoS};
    // TODO REVIEW: do we need this tokio mutex
    use tokio::sync::Mutex;
    use tokio::time;

    use crate::queue::{waking_map::WakingMap, Key};

    #[tokio::test]
    async fn insert() {
        let state = BTreeMap::new();
        let mut state = WakingMap::new(state);

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

        state.insert(key1.clone(), pub1.clone());
        let extracted = state.get_map().get(&key1).unwrap();
        assert_eq!(pub1, *extracted);
    }

    #[tokio::test]
    async fn remove() {
        let state = BTreeMap::new();
        let mut state = WakingMap::new(state);

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

        state.insert(key1.clone(), pub1.clone());

        let removed_pub = state.remove(key1.clone()).unwrap();
        assert_eq!(pub1, removed_pub);
    }

    // TODO REVIEW: replace wait with notify
    #[tokio::test]
    async fn insert_wakes_stream() {
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

        let map = BTreeMap::new();
        let map = WakingMap::new(map);
        let map = Arc::new(Mutex::new(map));

        // start reading stream in a separate thread
        // this stream will return pending until woken up
        let map_copy = Arc::clone(&map);
        let poll_stream = async move {
            let mut test_stream = TestStream::new(map_copy);
            assert_eq!(test_stream.next().await.unwrap(), 1);
        };

        let poll_stream_handle = tokio::spawn(poll_stream);
        time::delay_for(Duration::from_secs(2)).await;

        // insert an element to wake the stream, then wait for the other thread to complete
        map.lock().await.insert(key1, pub1);
        poll_stream_handle.await.unwrap();
    }

    struct TestStream {
        waking_map: Arc<Mutex<WakingMap>>,
        should_return_pending: bool,
    }

    impl TestStream {
        fn new(waking_map: Arc<Mutex<WakingMap>>) -> Self {
            TestStream {
                waking_map,
                should_return_pending: true,
            }
        }
    }

    impl Stream for TestStream {
        type Item = u32;

        fn poll_next(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
            let mut map_lock;
            let mut_self = self.get_mut();
            loop {
                if let Ok(lock) = mut_self.waking_map.try_lock() {
                    map_lock = lock;
                    break;
                }
            }

            if mut_self.should_return_pending {
                mut_self.should_return_pending = false;
                map_lock.set_waker(cx.waker());
                return Poll::Pending;
            } else {
                return Poll::Ready(Some(1));
            }
        }
    }
}
