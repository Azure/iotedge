#![allow(dead_code)] // TODO remove when ready

use std::{
    cmp::min, collections::hash_map::Entry, collections::HashMap, collections::VecDeque,
    task::Waker,
};

use async_trait::async_trait;
use bincode::{self};
use mqtt3::proto::Publication;
use rocksdb::{IteratorMode, Options, DB};
use uuid::Uuid;

use crate::persist::{Key, PersistError};

/// Responsible for waking waiting streams when new elements are added.
///
/// Exposes a get method for retrieving a count of elements in order of insertion.
#[async_trait]
pub trait StreamWakeableState {
    fn insert(&mut self, key: Key, value: Publication) -> Result<(), PersistError>;

    fn batch(&mut self, count: usize) -> Result<Vec<(Key, Publication)>, PersistError>;

    fn remove_in_flight(&mut self, key: &Key) -> Result<Publication, PersistError>;

    fn set_waker(&mut self, waker: &Waker);
}

/// When elements are retrieved they are moved to the in flight collection.
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
}

impl StreamWakeableState for WakingMap {
    fn insert(&mut self, key: Key, value: Publication) -> Result<(), PersistError> {
        self.queue.push_back((key, value));

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }

        Ok(())
    }

    fn batch(&mut self, count: usize) -> Result<Vec<(Key, Publication)>, PersistError> {
        let count = min(count, self.queue.len());
        let output: Vec<_> = self.queue.drain(..count).collect();
        self.in_flight.extend(output.clone().into_iter());

        Ok(output)
    }

    fn remove_in_flight(&mut self, key: &Key) -> Result<Publication, PersistError> {
        self.in_flight
            .remove(key)
            .ok_or(PersistError::RemovalForMissing())
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}

/// When elements are retrieved they are added to the in flight collection, but kept in the original db store.
/// Only when elements are removed from the in-flight collection they will be removed from the store.
pub struct WakingStore {
    db: DB,
    in_flight: HashMap<Key, Publication>,
    waker: Option<Waker>,
    column_family: String,
}

impl WakingStore {
    pub fn new(mut db: DB) -> Result<Self, PersistError> {
        let column_family = Uuid::new_v4().to_string();
        db.create_cf(column_family.clone(), &Options::default())
            .map_err(PersistError::CreateColumnFamily)?;

        Ok(Self {
            db,
            in_flight: HashMap::new(),
            waker: None,
            column_family,
        })
    }
}

impl StreamWakeableState for WakingStore {
    fn insert(&mut self, key: Key, value: Publication) -> Result<(), PersistError> {
        let key_bytes = bincode::serialize(&key).map_err(PersistError::Serialization)?;
        let publication_bytes = bincode::serialize(&value).map_err(PersistError::Serialization)?;

        let column_family = self
            .db
            .cf_handle(&self.column_family)
            .ok_or(PersistError::GetColumnFamily())?;
        self.db
            .put_cf(column_family, key_bytes, publication_bytes)
            .map_err(PersistError::Insertion)?;

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }

        Ok(())
    }

    /// Get count elements of store, exluding those that are already in in-flight
    fn batch(&mut self, count: usize) -> Result<Vec<(Key, Publication)>, PersistError> {
        let column_family = self
            .db
            .cf_handle(&self.column_family)
            .ok_or(PersistError::GetColumnFamily())?;
        let iter = self.db.iterator_cf(column_family, IteratorMode::Start);

        let mut output = vec![];
        for (iterations, extracted) in iter.enumerate() {
            let (key, publication) = bincode::deserialize(&*extracted.0)
                .map_err(PersistError::Deserialization)
                .and_then(|key: Key| {
                    bincode::deserialize(&extracted.1)
                        .map_err(PersistError::Deserialization)
                        .map(|publication: Publication| (key, publication))
                })?;

            if let Entry::Vacant(o) = self.in_flight.entry(key.clone()) {
                o.insert(publication.clone());
                output.push((key.clone(), publication.clone()));
                self.in_flight.insert(key, publication);
            }

            if iterations == count {
                break;
            }
        }

        Ok(output)
    }

    fn remove_in_flight(&mut self, key: &Key) -> Result<Publication, PersistError> {
        let key_bytes = bincode::serialize(&key).map_err(PersistError::Serialization)?;

        let column_family = self
            .db
            .cf_handle(&self.column_family)
            .ok_or(PersistError::GetColumnFamily())?;

        self.db
            .delete_cf(column_family, key_bytes)
            .map_err(PersistError::Removal)?;
        let removed = self
            .in_flight
            .remove(key)
            .ok_or(PersistError::RemovalForMissing())?;
        Ok(removed)
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}

#[cfg(test)]
mod tests {
    use std::{path::Path, pin::Pin, sync::Arc, task::Context, task::Poll};

    use bytes::Bytes;
    use futures_util::stream::{Stream, StreamExt};
    use matches::assert_matches;
    use mqtt3::proto::{Publication, QoS};
    use parking_lot::Mutex;
    use rocksdb::DB;
    use test_case::test_case;
    use tokio::sync::Notify;
    use uuid::Uuid;

    use crate::persist::{
        waking_state::WakingMap,
        waking_state::{StreamWakeableState, WakingStore},
        Key,
    };

    const STORAGE_DIR: &str = "unit-tests/persistence/";

    #[test_case(WakingMap::new())]
    #[test_case(init_rocksdb_test_store())]
    fn insert(mut state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1, pub1.clone()).unwrap();

        let current_state = state.batch(1).unwrap();
        let extracted_message = current_state.get(0).unwrap().1.clone();
        assert_eq!(pub1, extracted_message);
    }

    #[test_case(WakingMap::new())]
    #[test_case(init_rocksdb_test_store())]
    fn get_over_quantity(mut state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
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

    #[test_case(WakingMap::new())]
    #[test_case(init_rocksdb_test_store())]
    fn in_flight(mut state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1.clone(), pub1.clone()).unwrap();
        state.batch(1).unwrap();
        let removed = state.remove_in_flight(&key1).unwrap();
        assert_eq!(removed, pub1);
    }

    #[test_case(WakingMap::new())]
    #[test_case(init_rocksdb_test_store())]
    fn remove_in_flight_dne(mut state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let bad_removal = state.remove_in_flight(&key1);
        assert_matches!(bad_removal, Err(_));
    }

    #[test_case(WakingMap::new())]
    #[test_case(init_rocksdb_test_store())]
    fn remove_in_flight_inserted_but_not_yet_retrieved(mut state: impl StreamWakeableState) {
        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::new(),
        };

        state.insert(key1.clone(), pub1).unwrap();
        let bad_removal = state.remove_in_flight(&key1);
        assert_matches!(bad_removal, Err(_));
    }

    #[test_case(WakingMap::new())]
    #[test_case(init_rocksdb_test_store())]
    async fn insert_wakes_stream(state: impl StreamWakeableState + Send + 'static) {
        // setup data
        let state = Arc::new(Mutex::new(state));

        let key1 = Key { offset: 0 };
        let pub1 = Publication {
            topic_name: "test".to_string(),
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

    pub fn init_rocksdb_test_store() -> WakingStore {
        let mut storage_dir = STORAGE_DIR.to_string();
        let uuid = Uuid::new_v4().to_string();
        storage_dir.push_str(&uuid);
        let path = Path::new(&storage_dir);

        let db = DB::open_default(path).unwrap();
        WakingStore::new(db).unwrap()
    }
}
