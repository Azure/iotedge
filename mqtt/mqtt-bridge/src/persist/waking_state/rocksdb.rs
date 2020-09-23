use std::{
    collections::{HashMap, VecDeque},
    task::Waker,
    vec::IntoIter,
};

use bincode::{self};
use mqtt3::proto::Publication;
use rocksdb::{ColumnFamily, IteratorMode, Options, DB};

use crate::persist::{waking_state::StreamWakeableState, Key, PersistError};

/// The concept of maintaining a collection of loaded messages will prevent duplicates from being returned by batch().
///
/// When elements are retrieved they are added to the loaded collection, but kept in the original db store.
/// Only when elements are removed from the loaded collection they will be removed from the store.
///
/// If there is a crash then you lose state of what messages have been loaded, but the messages are still in the db.  
/// This is desirable since these messages are not guaranteed to be sent and thus should be given to the new loader and sent.
pub struct WakingRocksDBStore {
    db: RocksDbWrapper,
    loaded: HashMap<Key, Publication>,
    waker: Option<Waker>,
}

impl WakingRocksDBStore {
    pub fn new(db: DB, column_family: String) -> Result<Self, PersistError> {
        let db = RocksDbWrapper::new(db, column_family)?;

        Ok(Self {
            db,
            loaded: HashMap::new(),
            waker: None,
        })
    }
}

impl StreamWakeableState for WakingRocksDBStore {
    fn insert(&mut self, key: Key, value: Publication) -> Result<(), PersistError> {
        self.db.insert(key, &value)?;
        if let Some(waker) = self.waker.take() {
            waker.wake();
        }

        Ok(())
    }

    /// Get count elements of store, excluding those that have already been loaded
    fn batch(&mut self, count: usize) -> Result<VecDeque<(Key, Publication)>, PersistError> {
        let iter = self.db.iter_except(count, &self.loaded)?;

        self.loaded.extend(iter.clone());

        Ok(iter.collect())
    }

    fn remove(&mut self, key: Key) -> Result<Publication, PersistError> {
        let removed = self
            .loaded
            .remove(&key)
            .ok_or(PersistError::RemovalForMissing)?;
        self.db.remove(key)?;
        Ok(removed)
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.waker = Some(waker.clone());
    }
}

/// Wrapper around rocksdb database to abstract away serialization/deserialization
struct RocksDbWrapper {
    db: DB,
    column_family: String,
}

impl RocksDbWrapper {
    fn new(mut db: DB, column_family: String) -> Result<Self, PersistError> {
        db.create_cf(column_family.clone(), &Options::default())
            .map_err(PersistError::CreateColumnFamily)?;

        Ok(Self { db, column_family })
    }

    fn insert(&self, key: Key, publication: &Publication) -> Result<(), PersistError> {
        let key_bytes = bincode::serialize(&key).map_err(PersistError::Serialization)?;
        let publication_bytes =
            bincode::serialize(&publication).map_err(PersistError::Serialization)?;

        let column_family = self.column_family()?;
        self.db
            .put_cf(column_family, key_bytes, publication_bytes)
            .map_err(PersistError::Insertion)?;

        Ok(())
    }

    fn iter_except(
        &self,
        count: usize,
        exclude: &HashMap<Key, Publication>,
    ) -> Result<IntoIter<(Key, Publication)>, PersistError> {
        let column_family = self.column_family()?;
        let iter = self.db.iterator_cf(column_family, IteratorMode::Start);

        let mut iterations = 0;
        let mut output = vec![];
        for extracted in iter {
            let (key, publication) = bincode::deserialize(&*extracted.0)
                .map_err(PersistError::Deserialization)
                .and_then(|key: Key| {
                    bincode::deserialize(&extracted.1)
                        .map_err(PersistError::Deserialization)
                        .map(|publication: Publication| (key, publication))
                })?;

            if !exclude.contains_key(&key) {
                output.push((key, publication));
                iterations += 1;
            }

            if iterations == count {
                break;
            }
        }

        Ok(output.into_iter())
    }

    fn remove(&self, key: Key) -> Result<(), PersistError> {
        let key_bytes = bincode::serialize(&key).map_err(PersistError::Serialization)?;

        let column_family = self.column_family()?;

        self.db
            .delete_cf(column_family, key_bytes)
            .map_err(PersistError::Removal)?;
        Ok(())
    }

    fn column_family(&self) -> Result<&ColumnFamily, PersistError> {
        Ok(self
            .db
            .cf_handle(&self.column_family)
            .ok_or(PersistError::GetColumnFamily)?)
    }
}
