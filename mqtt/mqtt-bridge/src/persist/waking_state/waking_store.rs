use std::boxed::Box;
use std::{collections::HashMap, task::Waker};

use bincode::deserialize;
use bincode::serialize;
use bincode::ErrorKind;
use mqtt3::proto::Publication;
use rocksdb::IteratorMode;
use rocksdb::DB;
use tracing::error;

use crate::persist::waking_state::StreamWakeableState;
use crate::persist::Key;
use crate::persist::PersistError;

/// Responsible for waking waiting streams when new elements are added.
///
/// Exposes a get method for retrieving a count of elements in order of insertion.
/// When elements are retrieved they are added to the in flight collection, but kept in the original db store.
/// Only when elements are removed from the in-flight collection they will be removed from the store.
pub struct WakingStore {
    db: DB,
    in_flight: HashMap<Key, Publication>,
    waker: Option<Waker>,
}

impl WakingStore {
    pub fn new(db: DB) -> Self {
        let in_flight = HashMap::new();
        let waker = None;

        Self {
            db,
            in_flight,
            waker,
        }
    }
}

impl StreamWakeableState for WakingStore {
    fn insert(&mut self, key: Key, value: Publication) -> Result<(), PersistError> {
        let key_bytes = serialize(&key).map_err(PersistError::Serialization)?;
        let publication_bytes = serialize(&value).map_err(PersistError::Serialization)?;
        self.db
            .put(key_bytes, publication_bytes)
            .map_err(PersistError::Insertion)?;

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }

        Ok(())
    }

    /// Get count elements of store, exluding those that are already in in-flight
    fn get(&mut self, count: usize) -> Vec<(Key, Publication)> {
        let mut iter = self.db.iterator(IteratorMode::Start);
        let mut output = vec![];

        let mut iterations = 0;
        while let Some(extracted) = iter.next() {
            let key: Result<Key, Box<ErrorKind>> = deserialize(&*extracted.0);
            let key = match key {
                Ok(key) => key,
                Err(e) => {
                    error!(message = "failed to deserialize key", err = %e);
                    break;
                }
            };

            let publication: Result<Publication, Box<ErrorKind>> = deserialize(&*extracted.1);
            let publication = match publication {
                Ok(publication) => publication,
                Err(e) => {
                    error!(message = "failed to deserialize publication", err = %e);
                    break;
                }
            };

            if !self.in_flight.contains_key(&key) {
                output.push((key.clone(), publication.clone()));
                self.in_flight.insert(key, publication);
            }

            iterations += 1;
            if iterations == count {
                break;
            }
        }

        output
    }

    fn remove_in_flight(&mut self, key: &Key) -> Option<Publication> {
        let key_bytes = serialize(&key).unwrap();
        self.db.delete(key_bytes).unwrap();
        self.in_flight.remove(key)
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}
