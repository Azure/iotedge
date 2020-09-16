#![allow(dead_code)] // TODO remove when ready

use std::{collections::hash_map::Entry, collections::HashMap, task::Waker};

use bincode::{self};
use mqtt3::proto::Publication;
use rocksdb::{IteratorMode, DB};

use crate::persist::{waking_state::StreamWakeableState, Key, PersistError};

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
        Self {
            db,
            in_flight: HashMap::new(),
            waker: None,
        }
    }
}

impl StreamWakeableState for WakingStore {
    fn insert(&mut self, key: Key, value: Publication) -> Result<(), PersistError> {
        let key_bytes = bincode::serialize(&key).map_err(PersistError::Serialization)?;
        let publication_bytes = bincode::serialize(&value).map_err(PersistError::Serialization)?;
        self.db
            .put(key_bytes, publication_bytes)
            .map_err(PersistError::Insertion)?;

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }

        Ok(())
    }

    /// Get count elements of store, exluding those that are already in in-flight
    fn batch(&mut self, count: usize) -> Result<Vec<(Key, Publication)>, PersistError> {
        let iter = self.db.iterator(IteratorMode::Start);
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

    fn remove_in_flight(&mut self, key: &Key) -> Option<Publication> {
        let key_bytes = bincode::serialize(&key).unwrap();
        self.db.delete(key_bytes).unwrap();
        self.in_flight.remove(key)
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}
