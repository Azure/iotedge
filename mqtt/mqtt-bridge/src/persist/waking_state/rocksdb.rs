#![allow(dead_code)] // TODO remove when ready

use std::{
    collections::{hash_map::Entry, HashMap, VecDeque},
    task::Waker,
};

use bincode::{self};
use mqtt3::proto::Publication;
use rocksdb::{ColumnFamily, IteratorMode, Options, DB};
use uuid::Uuid;

use crate::persist::{waking_state::StreamWakeableState, Key, PersistError};

/// When elements are retrieved they are added to the in flight collection, but kept in the original db store.
/// Only when elements are removed from the in-flight collection they will be removed from the store.
pub struct WakingRocksDBStore {
    db: DB,
    in_flight: HashMap<Key, Publication>,
    waker: Option<Waker>,
    column_family: String,
}

impl WakingRocksDBStore {
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

    fn column_family(&self) -> Result<&ColumnFamily, PersistError> {
        Ok(self
            .db
            .cf_handle(&self.column_family)
            .ok_or(PersistError::GetColumnFamily)?)
    }
}

impl StreamWakeableState for WakingRocksDBStore {
    fn insert(&mut self, key: Key, value: Publication) -> Result<(), PersistError> {
        let key_bytes = bincode::serialize(&key).map_err(PersistError::Serialization)?;
        let publication_bytes = bincode::serialize(&value).map_err(PersistError::Serialization)?;

        let column_family = self.column_family()?;
        self.db
            .put_cf(column_family, key_bytes, publication_bytes)
            .map_err(PersistError::Insertion)?;

        if let Some(waker) = self.waker.take() {
            waker.wake();
        }

        Ok(())
    }

    /// Get count elements of store, exluding those that are already in in-flight
    fn batch(&mut self, count: usize) -> Result<VecDeque<(Key, Publication)>, PersistError> {
        let column_family = self.column_family()?;
        let iter = self.db.iterator_cf(column_family, IteratorMode::Start);

        let mut output = VecDeque::new();
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
                output.push_back((key.clone(), publication.clone()));
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

        let column_family = self.column_family()?;

        self.db
            .delete_cf(column_family, key_bytes)
            .map_err(PersistError::Removal)?;
        let removed = self
            .in_flight
            .remove(key)
            .ok_or(PersistError::RemovalForMissing)?;
        Ok(removed)
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}
