use async_trait::async_trait;
use mqtt3::proto::Publication;
use std::{collections::VecDeque, task::Waker};

use crate::persist::{Key, PersistError};

// pub mod memory;

// TODO: Currently rocksdb does not compile on musl.
//       Once we fix compilation we can add this module back.
//       If we decide fixing compilation is not efficient, we can reuse code in rocksdb.rs by substituting rocksdb wrapping abstraction.
// pub mod rocksdb;

/// Responsible for waking waiting streams when new elements are added.
/// Exposes a get method for retrieving a count of elements in order of insertion.
///
/// This trait is intended to be the shared state between a message store and a message loader abstraction.
///
/// All implementations of this trait should have the same behavior with respect to errors.
/// Thus all implementations will share the below test suite.
#[async_trait]
pub trait StreamWakeableState {
    async fn insert(&self, key: Key, value: Publication) -> Result<(), PersistError>;

    /// Get count elements of store, excluding those that have already been loaded
    async fn batch(&self, count: usize) -> Result<VecDeque<(Key, Publication)>, PersistError>;

    // This remove should error if the given element has not yet been returned by batch.
    async fn remove(&self, key: Key) -> Result<(), PersistError>;

    fn set_waker(&mut self, waker: &Waker);
}

#[cfg(test)]
mod tests {
    use async_trait::async_trait;
    use bytes::Bytes;
    use futures_util::stream::{Stream, StreamExt, TryStreamExt};
    use matches::assert_matches;
    use mqtt3::proto::{Publication, QoS};
    use parking_lot::Mutex;
    use rand::{distributions::Alphanumeric, thread_rng, Rng};
    use std::{collections::VecDeque, fs::{remove_dir_all, remove_file}, path::PathBuf, pin::Pin, sync::Arc, task::Context, task::Poll};
    use test_case::test_case;
    use tokio::{sync::Notify, task};

    use crate::persist::{Key, PersistError, loader::MessageLoader, storage::{ring_buffer::RingBuffer, /*sled::Sled,*/ FlushOptions}, waking_state::StreamWakeableState};

    const FLUSH_OPTIONS: FlushOptions = FlushOptions::Off;
    const FILE_NAME: &'static str = "test_file";
    const MAX_FILE_SIZE: usize = 1024 * 1024;

    fn cleanup_test_file(file_name: String) {
        let path = &PathBuf::from(file_name);
        if path.exists() {
            if path.is_file() {
                let result = remove_file(path);
                assert!(result.is_ok());
            }
            if path.is_dir() {
                let result = remove_dir_all(path);
                assert!(result.is_ok());
            }
        }
    }

    fn create_rand_str() -> String {
        thread_rng()
            .sample_iter(&Alphanumeric)
            .take(10)
            .map(char::from)
            .collect()
    }

    struct TestRingBuffer(RingBuffer, String);

    impl Default for TestRingBuffer {
        fn default() -> Self {
            let file_name = FILE_NAME.to_owned() + &create_rand_str();
            let rb = RingBuffer::new(file_name.clone(), MAX_FILE_SIZE, FLUSH_OPTIONS);
            TestRingBuffer(rb, file_name.clone())
        }
    }

    impl Drop for TestRingBuffer {
        fn drop(&mut self) {
            cleanup_test_file(self.1.clone())
        }
    }

    #[async_trait]
    impl StreamWakeableState for TestRingBuffer {
        async fn insert(&self, key: Key, value: Publication) -> Result<(), PersistError> {
            self.0.insert(key, value).await
        }

        async fn batch(&self, count: usize) -> Result<VecDeque<(Key, Publication)>, PersistError> {
            self.0.batch(count).await
        }

        async fn remove(&self, key: Key) -> Result<(), PersistError> {
            self.0.remove(key).await
        }

        fn set_waker(&mut self, waker: &std::task::Waker) {
            self.0.set_waker(waker)
        }
    }

    // struct TestSled(Sled, String, String);

    // impl Default for TestSled {
    //     fn default() -> Self {
    //         let file_name = FILE_NAME.to_owned() + &create_rand_str();
    //         let db_name = DB_PATH.to_owned() + &create_rand_str();
    //         let sled = Sled::new(db_name.clone(), file_name.clone(), FLUSH_OPTIONS);
    //         TestSled(sled, file_name.clone(), db_name.clone())
    //     }
    // }

    // impl Drop for TestSled {
    //     fn drop(&mut self) {
    //         cleanup_test_file(self.1.clone());
    //         cleanup_test_file(self.2.clone());
    //     }
    // }

    // impl StreamWakeableState for TestSled {
    //     fn insert(
    //         &mut self,
    //         key: Key,
    //         value: Publication,
    //     ) -> Result<(), crate::persist::PersistError> {
    //         self.0.insert(key, value)
    //     }

    //     fn batch(
    //         &mut self,
    //         count: usize,
    //     ) -> Result<std::collections::VecDeque<(Key, Publication)>, crate::persist::PersistError>
    //     {
    //         self.0.batch(count)
    //     }

    //     fn remove(&mut self, key: Key) -> Result<(), crate::persist::PersistError> {
    //         self.0.remove(key)
    //     }

    //     fn set_waker(&mut self, waker: &std::task::Waker) {
    //         self.0.set_waker(waker)
    //     }
    // }

    #[test_case(TestRingBuffer::default())]
    // #[test_case(TestSled::default())]
    // #[test_case(WakingMemoryStore::default())]
    #[tokio::test]
    async fn insert(state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1, pub1.clone()).await.unwrap();

        let current_state = state.batch(1).await.unwrap();
        assert!(!current_state.is_empty());
        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(TestRingBuffer::default())]
    // #[test_case(TestSled::default())]
    // #[test_case(WakingMemoryStore::default())]
    #[tokio::test]
    async fn ordering_maintained_across_insert(state: impl StreamWakeableState) {
        // insert a bunch of elements
        let num_elements = 10_usize;
        for i in 0..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let key = Key { offset: i as u64 };
            let publication = Publication {
                topic_name: i.to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            state.insert(key, publication).await.unwrap();
        }

        // verify they came back in the correct order
        let mut elements = state.batch(num_elements).await.unwrap();
        for count in 0..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let num_elements = count as u64;
            assert_eq!(elements.pop_front().unwrap().0.offset, num_elements)
        }
    }

    #[test_case(TestRingBuffer::default())]
    // #[test_case(TestSled::default())]
    // #[test_case(WakingMemoryStore::default())]
    #[tokio::test]
    async fn ordering_maintained_across_removal(state: impl StreamWakeableState) {
        // insert a bunch of elements
        let num_elements = 10_usize;
        for i in 0..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let key = Key { offset: i as u64 };
            let publication = Publication {
                topic_name: i.to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            state.insert(key, publication).await.unwrap();
        }

        // extract some, check that they are in order
        let state = Arc::new(state);
        let mut loader = MessageLoader::new(state.clone(), num_elements);
        let (key1, _) = loader.try_next().await.unwrap().unwrap();
        let (key2, _) = loader.try_next().await.unwrap().unwrap();
        assert_eq!(key1, Key { offset: 0 });
        assert_eq!(key2, Key { offset: 1 });

        // remove some
        state.remove(key1).await.unwrap();
        state.remove(key2).await.unwrap();

        // check that the ordering is maintained
        for count in 2..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let num_elements = count as u64;
            let extracted_offset = loader.try_next().await.unwrap().unwrap().0.offset;
            assert_eq!(extracted_offset, num_elements)
        }
    }

    #[test_case(TestRingBuffer::default())]
    // #[test_case(TestSled::default())]
    // #[test_case(WakingMemoryStore::default())]
    #[tokio::test]
    async fn larger_batch_size_respected(state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1, pub1.clone()).await.unwrap();

        let too_many_elements = 20;
        let current_state = state.batch(too_many_elements).await.unwrap();
        assert_eq!(current_state.len(), 1);

        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(TestRingBuffer::default())]
    // #[test_case(TestSled::default())]
    // #[test_case(WakingMemoryStore::default())]
    #[tokio::test]
    async fn smaller_batch_size_respected(state: impl StreamWakeableState) {
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

        state.insert(key1, pub1.clone()).await.unwrap();
        state.insert(key2, pub2).await.unwrap();

        let smaller_batch_size = 1;
        let current_state = state.batch(smaller_batch_size).await.unwrap();
        assert_eq!(current_state.len(), 1);

        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(TestRingBuffer::default())]
    // #[test_case(TestSled::default())]
    // #[test_case(WakingMemoryStore::default())]
    #[tokio::test]
    async fn remove_loaded(state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1, pub1).await.unwrap();
        state.batch(1).await.unwrap();
        assert_matches!(state.remove(key1).await, Ok(_));

        let empty_batch = state.batch(1).await.unwrap();
        assert_eq!(empty_batch.len(), 0);
    }

    #[test_case(TestRingBuffer::default())]
    // #[test_case(TestSled::default())]
    // #[test_case(WakingMemoryStore::default())]
    #[tokio::test]
    async fn remove_loaded_dne(state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let bad_removal = state.remove(key1).await;
        assert_matches!(bad_removal, Err(_));
    }

    #[test_case(TestRingBuffer::default())]
    // #[test_case(TestSled::default())]
    // #[test_case(WakingMemoryStore::default())]
    #[tokio::test]
    async fn remove_loaded_inserted_but_not_yet_retrieved(state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1, pub1).await.unwrap();
        let bad_removal = state.remove(key1).await;
        assert_matches!(bad_removal, Err(_));
    }

    #[test_case(TestRingBuffer::default())]
    // #[test_case(TestSled::default())]
    // #[test_case(WakingMemoryStore::default())]
    #[tokio::test]
    async fn remove_loaded_out_of_order(state: impl StreamWakeableState) {
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
        state.insert(key1, pub1).await.unwrap();
        state.insert(key2, pub2).await.unwrap();
        state.batch(2).await.unwrap();

        // remove out of order and verify
        assert_matches!(state.remove(key2).await, Ok(_))
    }

    #[ignore = "Seems to run indefinitely"]
    #[test_case(TestRingBuffer::default())]
    // #[test_case(TestSled::default())]
    // #[test_case(WakingMemoryStore::default())]
    // TODO: There is a clippy bug where it shows false positive for this rule.
    // When this issue is closed remove this allow.
    // https://github.com/rust-lang/rust-clippy/issues/6353
    #[allow(clippy::await_holding_refcell_ref)]
    #[tokio::test]
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
        let state_copy = state.clone();
        let notify = Arc::new(Notify::new());
        let notify2 = notify.clone();
        let poll_stream = async move {
            let mut test_stream = TestStream::new(state_copy, notify2);
            assert_eq!(test_stream.next().await.unwrap(), 1);
        };

        let local = task::LocalSet::new();
        local
            .run_until(async move {
                let poll_stream_handle = task::spawn_local(poll_stream);
                notify.notified().await;

                // insert an element to wake the stream, then wait for the other thread to complete
                let state = state.lock();
                state.insert(key1, pub1).await.unwrap();
                poll_stream_handle.await.unwrap();
            })
            .await;
    }

    struct TestStream<S: StreamWakeableState> {
        _state: Arc<Mutex<S>>,
        notify: Arc<Notify>,
        should_return_pending: bool,
    }

    impl<S: StreamWakeableState> TestStream<S> {
        fn new(state: Arc<Mutex<S>>, notify: Arc<Notify>) -> Self {
            TestStream {
                _state: state,
                notify,
                should_return_pending: true,
            }
        }
    }

    impl<S: StreamWakeableState> Stream for TestStream<S> {
        type Item = u32;

        fn poll_next(self: Pin<&mut Self>, _cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
            let mut_self = self.get_mut();
            // let state = mut_self.state.lock();

            if mut_self.should_return_pending {
                mut_self.should_return_pending = false;
                // state.set_waker(cx.waker());
                mut_self.notify.notify();
                Poll::Pending
            } else {
                Poll::Ready(Some(1))
            }
        }
    }
}
