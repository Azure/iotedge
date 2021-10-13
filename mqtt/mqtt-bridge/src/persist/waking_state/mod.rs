use std::{collections::VecDeque, task::Waker};

use mqtt3::proto::Publication;

use crate::persist::{Key, PersistResult};

pub mod memory;
pub mod ring_buffer;

/// Responsible for waking waiting streams when new elements are added.
/// Exposes a get method for retrieving a count of elements in order of insertion.
///
/// This trait is intended to be the shared state between a message store and a message loader abstraction.
///
/// All implementations of this trait should have the same behavior with respect to errors.
/// Thus all implementations will share the below test suite.
pub trait StreamWakeableState {
    /// Inserts a publication to the queue.
    /// Returns the key of the inserted publication.
    fn insert(&mut self, value: &Publication) -> PersistResult<Key>;

    /// Returns a batch of elements in order of insertion.
    fn batch(&mut self, size: usize) -> PersistResult<VecDeque<(Key, Publication)>>;

    /// Removes the oldest publication from the queue.
    /// This remove should error if the given element has not yet been returned by batch.
    fn pop(&mut self) -> PersistResult<Key>;

    fn set_waker(&mut self, waker: &Waker);
}

#[cfg(test)]
#[allow(clippy::semicolon_if_nothing_returned)]
mod tests {
    use std::{num::NonZeroUsize, pin::Pin, sync::Arc, task::Context, task::Poll};

    use bytes::Bytes;
    use futures_util::stream::{Stream, StreamExt, TryStreamExt};
    use matches::assert_matches;
    use mqtt3::proto::{Publication, QoS};
    use parking_lot::Mutex;
    use test_case::test_case;
    use tokio::{
        sync::Notify,
        task::{self, LocalSet},
    };

    use crate::persist::{
        loader::MessageLoader,
        waking_state::{
            memory::test::TestWakingMemoryStore, ring_buffer::test::TestRingBuffer,
            StreamWakeableState,
        },
    };

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    fn insert(mut state: impl StreamWakeableState) {
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let _key1 = state.insert(&pub1).unwrap();

        let current_state = state.batch(1).unwrap();
        assert!(!current_state.is_empty());
        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    fn ordering_maintained_across_insert(mut state: impl StreamWakeableState) {
        // insert a bunch of elements
        let num_elements = 10_usize;
        let mut keys = vec![];
        for i in 0..num_elements {
            let publication = Publication {
                topic_name: i.to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            let key = state.insert(&publication).unwrap();
            keys.push(key);
        }

        // verify they came back in the correct order
        let mut elements = state.batch(num_elements).unwrap();
        for key in keys {
            assert_eq!(elements.pop_front().unwrap().0.offset, key.offset)
        }
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    #[tokio::test]
    async fn ordering_maintained_across_removal(mut state: impl StreamWakeableState) {
        // insert a bunch of elements
        let num_elements = 10_usize;
        let mut keys = vec![];
        for i in 0..num_elements {
            let publication = Publication {
                topic_name: i.to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::new(),
            };

            let key = state.insert(&publication).unwrap();
            keys.push(key);
        }

        // extract some, check that they are in order
        let state = Arc::new(Mutex::new(state));
        let mut loader =
            MessageLoader::new(state.clone(), NonZeroUsize::new(num_elements).unwrap());
        let (key1, _) = loader.try_next().await.unwrap().unwrap();
        let (key2, _) = loader.try_next().await.unwrap().unwrap();
        assert_eq!(key1, keys[0]);
        assert_eq!(key2, keys[1]);

        // remove some
        let key = state.lock().pop().unwrap();
        assert_eq!(key, key1);

        let key = state.lock().pop().unwrap();
        assert_eq!(key, key2);

        // check that the ordering is maintained
        for key in &keys[2..] {
            let extracted_offset = loader.try_next().await.unwrap().unwrap().0.offset;
            assert_eq!(extracted_offset, key.offset)
        }
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    fn larger_batch_size_respected(mut state: impl StreamWakeableState) {
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let _key1 = state.insert(&pub1).unwrap();

        let too_many_elements = 20;
        let current_state = state.batch(too_many_elements).unwrap();
        assert_eq!(current_state.len(), 1);

        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    fn smaller_batch_size_respected(mut state: impl StreamWakeableState) {
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

        let _key1 = state.insert(&pub1).unwrap();
        let _key2 = state.insert(&pub2).unwrap();

        let smaller_batch_size = 1;
        let current_state = state.batch(smaller_batch_size).unwrap();
        assert_eq!(current_state.len(), 1);

        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    fn remove_loaded(mut state: impl StreamWakeableState) {
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let key1 = state.insert(&pub1).unwrap();
        state.batch(1).unwrap();
        assert_matches!(state.pop(), Ok(key) if key == key1);

        let result = state.batch(1);
        assert_matches!(result, Ok(_));
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    fn remove_loaded_dne(mut state: impl StreamWakeableState) {
        let bad_removal = state.pop();
        assert_matches!(bad_removal, Err(_));
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
    fn remove_loaded_inserted_but_not_yet_retrieved(mut state: impl StreamWakeableState) {
        let pub1 = Publication {
            topic_name: "1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        let _ = state.insert(&pub1).unwrap();
        let bad_removal = state.pop();
        assert_matches!(bad_removal, Err(_));
    }

    #[test_case(TestRingBuffer::default())]
    #[test_case(TestWakingMemoryStore::default())]
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

        let local = LocalSet::new();
        local
            .run_until(async move {
                let handle = task::spawn_local(poll_stream);
                notify.notified().await;
                {
                    // insert an element to wake the stream, then wait for the other thread to complete
                    let mut state = state.lock();
                    let _key = state.insert(&pub1).unwrap();
                }
                handle.await.unwrap();
            })
            .await;
    }

    struct TestStream<S: StreamWakeableState> {
        state: Arc<Mutex<S>>,
        notify: Arc<Notify>,
        should_return_pending: bool,
    }

    impl<S: StreamWakeableState> TestStream<S> {
        fn new(state: Arc<Mutex<S>>, notify: Arc<Notify>) -> Self {
            TestStream {
                state,
                notify,
                should_return_pending: true,
            }
        }
    }

    impl<S: StreamWakeableState> Stream for TestStream<S> {
        type Item = u32;

        fn poll_next(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
            let mut_self = self.get_mut();
            let mut state = mut_self.state.lock();
            if mut_self.should_return_pending {
                mut_self.should_return_pending = false;
                state.set_waker(cx.waker());
                mut_self.notify.notify_one();
                Poll::Pending
            } else {
                Poll::Ready(Some(1))
            }
        }

        fn size_hint(&self) -> (usize, Option<usize>) {
            (0, None)
        }
    }
}
