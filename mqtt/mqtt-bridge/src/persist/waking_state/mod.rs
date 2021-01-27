use mqtt3::proto::Publication;
use std::{collections::VecDeque, future::Future, pin::Pin};

use crate::persist::Key;

pub mod memory;
pub mod ring_buffer;

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
pub trait StreamWakeableState {
    fn insert<'a>(
        &mut self,
        value: Publication,
    ) -> Pin<Box<dyn Future<Output = Result<Key, StorageError>> + Send + 'a>>;

    /// Get count elements of store, excluding those that have already been loaded
    fn batch<'a>(
        &mut self,
        count: usize,
    ) -> Pin<Box<dyn Future<Output = Result<VecDeque<(Key, Publication)>, StorageError>> + Send + 'a>>;

    // This remove should error if the given element has not yet been returned by batch.
    fn remove(&mut self, key: Key) -> Result<(), StorageError>;
}

#[cfg(test)]
mod tests {
    use bytes::Bytes;
    use futures_util::{
        stream::{Stream, StreamExt, TryStreamExt},
        task::noop_waker_ref,
    };
    use matches::assert_matches;
    use mqtt3::proto::{Publication, QoS};
    use parking_lot::Mutex;
    use rand::{distributions::Alphanumeric, thread_rng, Rng};
    use std::{
        collections::VecDeque,
        fs::{remove_dir_all, remove_file},
        future::Future,
        path::PathBuf,
        pin::Pin,
        sync::Arc,
        task::Context,
        task::Poll,
    };
    use test_case::test_case;
    use tokio::{sync::Notify, task};

    use crate::persist::{
        loader::MessageLoader,
        storage::{ring_buffer::RingBufferStorage, /*sled::Sled,*/ FlushOptions},
        waking_state::StreamWakeableState,
        Key, StorageError,
    };

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

    struct TestRingBuffer(RingBufferStorage, String);

    impl Default for TestRingBuffer {
        fn default() -> Self {
            let file_name = FILE_NAME.to_owned() + &create_rand_str();
            let rb = RingBufferStorage::new(file_name.clone(), MAX_FILE_SIZE, FLUSH_OPTIONS);
            TestRingBuffer(rb, file_name.clone())
        }
    }

    impl Drop for TestRingBuffer {
        fn drop(&mut self) {
            cleanup_test_file(self.1.clone())
        }
    }

    impl StreamWakeableState for TestRingBuffer {
        fn insert<'a>(
            &mut self,
            value: Publication,
        ) -> Pin<Box<dyn Future<Output = Result<Key, StorageError>> + Send + 'a>> {
            self.0.insert(value)
        }

        fn batch<'a>(
            &mut self,
            count: usize,
        ) -> Pin<
            Box<
                dyn Future<Output = Result<VecDeque<(Key, Publication)>, StorageError>> + Send + 'a,
            >,
        > {
            self.0.batch(count)
        }

        fn remove(&mut self, key: Key) -> Result<(), StorageError> {
            self.0.remove(key)
        }
    }

    #[test_case(TestRingBuffer::default())]
    #[tokio::test]
    async fn insert(mut state: impl StreamWakeableState) {
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let _key1 = state.insert(pub1.clone()).await.unwrap();

        let current_state = state.batch(1).await.unwrap();
        assert!(!current_state.is_empty());
        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(TestRingBuffer::default())]
    #[tokio::test]
    async fn ordering_maintained_across_insert(mut state: impl StreamWakeableState) {
        // insert a bunch of elements
        let num_elements = 10_usize;
        let mut keys = vec![];
        for i in 0..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let publication = Publication {
                topic_name: i.to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            let key = state.insert(publication).await.unwrap();
            keys.push(key);
        }

        // verify they came back in the correct order
        let mut elements = state.batch(num_elements).await.unwrap();
        for key in keys {
            #[allow(clippy::cast_possible_truncation)]
            assert_eq!(elements.pop_front().unwrap().0, key)
        }
    }

    #[test_case(TestRingBuffer::default())]
    #[tokio::test]
    async fn ordering_maintained_across_removal(mut state: impl StreamWakeableState) {
        // insert a bunch of elements
        let num_elements = 10_usize;
        let mut keys = vec![];
        for i in 0..num_elements {
            #[allow(clippy::cast_possible_truncation)]
            let publication = Publication {
                topic_name: i.to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            let key = state.insert(publication).await.unwrap();
            keys.push(key);
        }

        // extract some, check that they are in order
        let state = Arc::new(Mutex::new(state));
        let mut loader = MessageLoader::new(state.clone(), num_elements);
        let (key1, _) = loader.try_next().await.unwrap().unwrap();
        let (key2, _) = loader.try_next().await.unwrap().unwrap();
        assert_eq!(key1, keys[0]);
        assert_eq!(key2, keys[1]);

        // remove some
        {
            let mut borrowed_state = state.lock();
            borrowed_state.remove(key1).unwrap();
            borrowed_state.remove(key2).unwrap();
        }
        // check that the ordering is maintained
        for key in &keys[2..] {
            #[allow(clippy::cast_possible_truncation)]
            let key_received = loader.try_next().await.unwrap().unwrap().0;
            assert_eq!(*key, key_received);
        }
    }

    #[test_case(TestRingBuffer::default())]
    #[tokio::test]
    async fn larger_batch_size_respected(mut state: impl StreamWakeableState) {
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let _key1 = state.insert(pub1.clone()).await.unwrap();

        let too_many_elements = 20;
        let current_state = state.batch(too_many_elements).await.unwrap();
        assert_eq!(current_state.len(), 1);

        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(TestRingBuffer::default())]
    #[tokio::test]
    async fn smaller_batch_size_respected(mut state: impl StreamWakeableState) {
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

        let _key1 = state.insert(pub1.clone()).await.unwrap();
        let _key2 = state.insert(pub2).await.unwrap();

        let smaller_batch_size = 1;
        let current_state = state.batch(smaller_batch_size).await.unwrap();
        assert_eq!(current_state.len(), 1);

        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(TestRingBuffer::default())]
    #[tokio::test]
    async fn remove_loaded(mut state: impl StreamWakeableState) {
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let key1 = state.insert(pub1).await.unwrap();
        state.batch(1).await.unwrap();
        assert_matches!(state.remove(key1), Ok(_));

        let mut dummy_context = Context::from_waker(noop_waker_ref());
        let poll = state.batch(1).as_mut().poll(&mut dummy_context);
        assert_matches!(poll, Poll::Pending);
    }

    #[test_case(TestRingBuffer::default())]
    #[tokio::test]
    async fn remove_loaded_dne(mut state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let bad_removal = state.remove(key1);
        assert_matches!(bad_removal, Err(_));
    }

    #[test_case(TestRingBuffer::default())]
    #[tokio::test]
    async fn remove_loaded_inserted_but_not_yet_retrieved(mut state: impl StreamWakeableState) {
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let key1 = state.insert(pub1).await.unwrap();
        let bad_removal = state.remove(key1);
        assert_matches!(bad_removal, Err(_));
    }

    #[test_case(TestRingBuffer::default())]
    #[tokio::test]
    async fn remove_loaded_out_of_order(mut state: impl StreamWakeableState) {
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

        // insert elements and extract
        let _key1 = state.insert(pub1).await.unwrap();
        let key2 = state.insert(pub2).await.unwrap();
        state.batch(2).await.unwrap();

        // remove out of order and verify
        assert_matches!(state.remove(key2), Err(_))
    }

    #[test_case(TestRingBuffer::default())]
    // TODO: There is a clippy bug where it shows false positive for this rule.
    // When this issue is closed remove this allow.
    // https://github.com/rust-lang/rust-clippy/issues/6353
    #[allow(clippy::await_holding_refcell_ref)]
    #[tokio::test]
    async fn insert_wakes_stream(state: impl StreamWakeableState + Send + 'static) {
        // setup data
        let state = Arc::new(Mutex::new(state));

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
                let mut state = state.lock();
                let _key = state.insert(pub1).await.unwrap();
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
