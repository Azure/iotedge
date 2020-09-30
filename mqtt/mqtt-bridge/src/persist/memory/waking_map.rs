use std::{cmp::min, collections::HashMap, collections::VecDeque, task::Waker};

use mqtt3::proto::Publication;
use tracing::error;

use crate::persist::Key;

/// Responsible for waking waiting streams when new elements are added
/// Exposes a get method for retrieving a count of elements starting from queue head
/// Once elements are retrieved they are added to the in flight collection
pub struct WakingMap {
    queue: VecDeque<(Key, Publication)>,
    in_flight: HashMap<Key, Publication>,
    waker: Option<Waker>,
}

impl WakingMap {
    pub fn new() -> Self {
        let queue: VecDeque<(Key, Publication)> = VecDeque::new();
        let in_flight = HashMap::new();

        WakingMap {
            queue,
            in_flight,
            waker: None,
        }
    }

    pub fn insert(&mut self, key: Key, value: Publication) {
        self.queue.push_back((key, value));

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }
    }

    pub fn get(&mut self, count: usize) -> Vec<(Key, Publication)> {
        let count = min(count, self.queue.len());
        let mut output = vec![];
        for _ in 0..count {
            let removed = self.queue.pop_front();

            if let Some(pair) = removed {
                output.push((pair.0.clone(), pair.1.clone()));
                self.in_flight.insert(pair.0, pair.1);
            } else {
                error!("failed retrieving message from persistence");
                continue;
            }
        }

        output
    }

    pub fn remove_in_flight(&mut self, key: &Key) -> Option<Publication> {
        self.in_flight.remove(key)
    }

    pub fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}

#[cfg(test)]
mod tests {
    use std::{pin::Pin, sync::Arc, task::Context, task::Poll};

    use bytes::Bytes;
    use futures_util::stream::{Stream, StreamExt};
    use matches::assert_matches;
    use mqtt3::proto::{Publication, QoS};
    use parking_lot::Mutex;
    use tokio::sync::Notify;

    use crate::persist::{memory::waking_map::WakingMap, Key};

    #[test]
    fn insert() {
        let mut state = WakingMap::new();

        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1, pub1.clone());

        let current_state = state.get(1);
        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test]
    fn get_over_quantity_succeeds() {
        let mut state = WakingMap::new();

        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1, pub1.clone());

        let too_many_elements = 20;
        let current_state = state.get(too_many_elements);
        assert_eq!(current_state.len(), 1);

        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test]
    fn in_flight() {
        let mut state = WakingMap::new();

        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        assert_eq!(state.in_flight.len(), 0);
        state.insert(key1.clone(), pub1.clone());
        assert_eq!(state.in_flight.len(), 0);

        state.get(1);
        assert_eq!(state.in_flight.len(), 1);

        let removed = state.remove_in_flight(&key1).unwrap();
        assert_eq!(removed, pub1);
        assert_eq!(state.in_flight.len(), 0);
    }

    #[test]
    fn remove_in_flight_dne() {
        let mut state = WakingMap::new();

        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1.clone(), pub1);
        let bad_removal = state.remove_in_flight(&key1);
        assert_matches!(bad_removal, None);
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

        let state = WakingMap::new();
        let state = Arc::new(Mutex::new(state));

        // start reading stream in a separate thread
        // this stream will return pending until woken up
        let map_copy = Arc::clone(&state);
        let notify = Arc::new(Notify::new());
        let notify2 = notify.clone();
        let poll_stream = async move {
            let mut test_stream = TestStream::new(map_copy, notify2);
            assert_eq!(test_stream.next().await.unwrap(), 1);
        };

        let poll_stream_handle = tokio::spawn(poll_stream);
        notify.notified().await;

        // insert an element to wake the stream, then wait for the other thread to complete
        state.lock().insert(key1, pub1);
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
