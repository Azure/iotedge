use std::{collections::BTreeMap, task::Waker};

use mqtt3::proto::Publication;

use crate::persist::Key;

// Responsible for waking waiting streams when new elements are added
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

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }
    }

    pub fn remove(&mut self, key: &Key) -> Option<Publication> {
        self.map.remove(&key)
    }

    // exposed for specific loading logic
    pub fn map(&self) -> &BTreeMap<Key, Publication> {
        &self.map
    }

    pub fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}

#[cfg(test)]
mod tests {
    use std::{collections::BTreeMap, pin::Pin, sync::Arc, task::Context, task::Poll};

    use bytes::Bytes;
    use futures_util::stream::{Stream, StreamExt};
    use mqtt3::proto::{Publication, QoS};
    use parking_lot::Mutex;
    use tokio::sync::Notify;

    use crate::persist::{memory::waking_map::WakingMap, Key};

    #[test]
    fn insert() {
        let state = BTreeMap::new();
        let mut state = WakingMap::new(state);

        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1.clone(), pub1.clone());
        let extracted = state.map().get(&key1).unwrap();
        assert_eq!(pub1, *extracted);
    }

    #[test]
    fn remove() {
        let state = BTreeMap::new();
        let mut state = WakingMap::new(state);

        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1.clone(), pub1.clone());

        let removed_pub = state.remove(&key1).unwrap();
        assert_eq!(pub1, removed_pub);
    }

    #[tokio::test]
    async fn insert_wakes_stream() {
        // setup data
        let key1 = Key { offset: 0 };
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
        let notify = Arc::new(Notify::new());
        let notify2 = notify.clone();
        let poll_stream = async move {
            let mut test_stream = TestStream::new(map_copy, notify2);
            assert_eq!(test_stream.next().await.unwrap(), 1);
        };

        let poll_stream_handle = tokio::spawn(poll_stream);
        notify.notified().await;

        // insert an element to wake the stream, then wait for the other thread to complete
        map.lock().insert(key1, pub1);
        poll_stream_handle.await.unwrap();
    }

    struct TestStream {
        waking_map: Arc<Mutex<WakingMap>>,
        notify: Arc<Notify>,
        should_return_pending: bool,
    }

    impl TestStream {
        fn new(waking_map: Arc<Mutex<WakingMap>>, notify: Arc<Notify>) -> Self {
            TestStream {
                waking_map,
                notify,
                should_return_pending: true,
            }
        }
    }

    impl Stream for TestStream {
        type Item = u32;

        fn poll_next(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
            let mut_self = self.get_mut();
            let mut map_lock = mut_self.waking_map.lock();

            if mut_self.should_return_pending {
                mut_self.should_return_pending = false;
                map_lock.set_waker(cx.waker());
                mut_self.notify.notify();
                Poll::Pending
            } else {
                Poll::Ready(Some(1))
            }
        }
    }
}
