use std::collections::VecDeque;
use std::task::Waker;

use async_trait::async_trait;
use mqtt3::proto::Publication;

use crate::persist::{Key, PersistError};

pub mod memory;
pub mod rocksdb;
/// Responsible for waking waiting streams when new elements are added.
///
/// Exposes a get method for retrieving a count of elements in order of insertion.
#[async_trait]
pub trait StreamWakeableState {
    fn insert(&mut self, key: Key, value: Publication) -> Result<(), PersistError>;

    fn batch(&mut self, count: usize) -> Result<VecDeque<(Key, Publication)>, PersistError>;

    fn remove_in_flight(&mut self, key: &Key) -> Result<Publication, PersistError>;

    fn set_waker(&mut self, waker: &Waker);
}

#[cfg(test)]
mod tests {
    use std::{pin::Pin, sync::Arc, task::Context, task::Poll};

    use bytes::Bytes;
    use futures_util::stream::{Stream, StreamExt};
    use matches::assert_matches;
    use mqtt3::proto::{Publication, QoS};
    use parking_lot::Mutex;
    use rocksdb::DB;
    use tempfile::TempDir;
    use test_case::test_case;
    use tokio::sync::Notify;

    use crate::persist::{
        loader::MessageLoader, waking_state::StreamWakeableState, Key, WakingMemoryStore,
        WakingRocksDBStore,
    };

    #[test_case(WakingMemoryStore::new())]
    #[test_case(init_rocksdb_test_store())]
    fn insert(mut state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1, pub1.clone()).unwrap();

        let current_state = state.batch(1).unwrap();
        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(WakingMemoryStore::new())]
    #[test_case(init_rocksdb_test_store())]
    fn ordering_maintained_across_insert(mut state: impl StreamWakeableState) {
        // insert a bunch of elements
        let num_elements = 10 as usize;
        for i in 0..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let key = Key { offset: i as u32 };
            let publication = Publication {
                topic_name: i.to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            state.insert(key, publication).unwrap();
        }

        // verify they came back in the correct order
        let mut elements = state.batch(num_elements).unwrap();
        for count in 0..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let num_elements = count as u32;
            assert_eq!(elements.pop_front().unwrap().0.offset, num_elements)
        }
    }

    #[test_case(WakingMemoryStore::new())]
    #[test_case(init_rocksdb_test_store())]
    async fn ordering_maintained_across_removal(mut state: impl StreamWakeableState) {
        // insert a bunch of elements
        let num_elements = 10 as usize;
        for i in 0..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let key = Key { offset: i as u32 };
            let publication = Publication {
                topic_name: i.to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            state.insert(key, publication).unwrap();
        }

        // extract some, check that they are in order
        let state_lock = Arc::new(Mutex::new(state));
        let mut loader = MessageLoader::new(state_lock.clone(), num_elements);
        let (key1, _) = loader.next().await.unwrap();
        let (key2, _) = loader.next().await.unwrap();
        assert_eq!(key1, Key { offset: 0 });
        assert_eq!(key2, Key { offset: 1 });

        // remove some
        state_lock.lock().remove_in_flight(&key1).unwrap();
        state_lock.lock().remove_in_flight(&key2).unwrap();

        // check that the ordering is maintained
        for count in 2..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let num_elements = count as u32;
            let extracted_offset = loader.next().await.unwrap().0.offset;
            assert_eq!(extracted_offset, num_elements)
        }
    }

    #[test_case(WakingMemoryStore::new())]
    #[test_case(init_rocksdb_test_store())]
    fn larger_batch_size_respected(mut state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1, pub1.clone()).unwrap();

        let too_many_elements = 20;
        let current_state = state.batch(too_many_elements).unwrap();
        assert_eq!(current_state.len(), 1);

        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(WakingMemoryStore::new())]
    #[test_case(init_rocksdb_test_store())]
    fn smaller_batch_size_respected(mut state: impl StreamWakeableState) {
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

        state.insert(key1, pub1.clone()).unwrap();
        state.insert(key2, pub2.clone()).unwrap();

        let smaller_batch_size = 1;
        let current_state = state.batch(smaller_batch_size).unwrap();
        assert_eq!(current_state.len(), 1);

        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(WakingMemoryStore::new())]
    #[test_case(init_rocksdb_test_store())]
    async fn in_flight(mut state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1.clone(), pub1.clone()).unwrap();

        let state_lock = Arc::new(Mutex::new(state));
        let mut loader = MessageLoader::new(state_lock.clone(), 1);
        let (key1, _) = loader.next().await.unwrap();
        assert_eq!(key1, Key { offset: 0 });

        let removed = state_lock.lock().remove_in_flight(&key1).unwrap();
        assert_eq!(removed, pub1);
    }

    #[test_case(WakingMemoryStore::new())]
    #[test_case(init_rocksdb_test_store())]
    fn remove_in_flight_dne(mut state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let bad_removal = state.remove_in_flight(&key1);
        assert_matches!(bad_removal, Err(_));
    }

    #[test_case(WakingMemoryStore::new())]
    #[test_case(init_rocksdb_test_store())]
    fn remove_in_flight_inserted_but_not_yet_retrieved(mut state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1.clone(), pub1).unwrap();
        let bad_removal = state.remove_in_flight(&key1);
        assert_matches!(bad_removal, Err(_));
    }

    #[test_case(WakingMemoryStore::new())]
    #[test_case(init_rocksdb_test_store())]
    async fn remove_in_flight_out_of_order(mut state: impl StreamWakeableState) {
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

        // insert elements and extract
        state.insert(key1.clone(), pub1.clone()).unwrap();
        state.insert(key2.clone(), pub2.clone()).unwrap();

        let state_lock = Arc::new(Mutex::new(state));
        let mut loader = MessageLoader::new(state_lock.clone(), 1);
        let (key1, _) = loader.next().await.unwrap();
        let (key2, _) = loader.next().await.unwrap();
        assert_eq!(key1, Key { offset: 0 });
        assert_eq!(key2, Key { offset: 1 });

        let extracted_pub2 = state_lock.lock().remove_in_flight(&key2).unwrap();
        let extracted_pub1 = state_lock.lock().remove_in_flight(&key1).unwrap();

        assert_eq!(extracted_pub2, pub2);
        assert_eq!(extracted_pub1, pub1);
    }

    #[test_case(WakingMemoryStore::new())]
    #[test_case(init_rocksdb_test_store())]
    async fn insert_wakes_stream(state: impl StreamWakeableState + Send + 'static) {
        // setup data
        let state = Arc::new(Mutex::new(state));

        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        // start reading stream in a separate thread
        // this stream will return pending until woken up
        let state_copy = Arc::clone(&state);
        let notify = Arc::new(Notify::new());
        let notify2 = notify.clone();
        let poll_stream = async move {
            let mut test_stream = TestStream::new(state_copy, notify2);
            assert_eq!(test_stream.next().await.unwrap(), 1);
        };

        let poll_stream_handle = tokio::spawn(poll_stream);
        notify.notified().await;

        // insert an element to wake the stream, then wait for the other thread to complete
        state.lock().insert(key1, pub1).unwrap();
        poll_stream_handle.await.unwrap();
    }

    struct TestStream<S: StreamWakeableState> {
        state: Arc<Mutex<S>>,
        notify: Arc<Notify>,
        should_return_pending: bool,
    }

    impl<S: StreamWakeableState> TestStream<S> {
        fn new(waking_map: Arc<Mutex<S>>, notify: Arc<Notify>) -> Self {
            TestStream {
                state: waking_map,
                notify,
                should_return_pending: true,
            }
        }
    }

    impl<S: StreamWakeableState> Stream for TestStream<S> {
        type Item = u32;

        fn poll_next(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
            let mut_self = self.get_mut();
            let mut map_lock = mut_self.state.lock();

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

    pub fn init_rocksdb_test_store() -> WakingRocksDBStore {
        let tmp_dir = TempDir::new().unwrap();
        let path = tmp_dir.path().to_owned();

        let db = DB::open_default(path).unwrap();
        WakingRocksDBStore::new(db).unwrap()
    }
}
