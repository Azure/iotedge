pub mod error;
pub mod rocksdb;
pub mod sled;

use self::{error::DbError, rocksdb::Rocksdb, sled::Sled};
use crate::{Storage, StorageError, StorageResult};
use std::{collections::VecDeque, path::PathBuf, ops::Deref};

pub type DbResult<T> = Result<T, DbError>;

#[derive(Clone)]
pub enum DbType {
    RocksDb(PathBuf),
    Sled(PathBuf),
}

pub struct Db(Box<dyn EmbeddedDatabase>);

unsafe impl Send for Db {}
unsafe impl Sync for Db {}

impl Deref for Db {
    type Target = Box<dyn EmbeddedDatabase>;

    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

pub fn create_embedded_database(db: DbType) -> DbResult<Db> {
    let inner: Box<dyn EmbeddedDatabase> = match db {
        DbType::RocksDb(path) => Box::new(Rocksdb::new(path)),
        DbType::Sled(path) => Box::new(Sled::new(path)),
    };
    Ok(Db(inner))
}

pub trait EmbeddedDatabase: Send + Sync {
    fn init(&mut self, names: Vec<String>) -> DbResult<()>;

    fn save(&self, queue_name: &str, value: &Vec<u8>) -> DbResult<usize>;

    fn load(&self, queue_name: &str) -> DbResult<Option<(usize, Vec<u8>)>>;

    fn batch(&self, queue_name: &str, size: usize) -> DbResult<VecDeque<(usize, Vec<u8>)>>;

    fn remove(&self, queue_name: &str, key: &usize) -> DbResult<()>;
}

impl Storage for Db
{
    type Key = usize;
    type Value = Vec<u8>;

    fn init(&mut self, names: Vec<String>) -> StorageResult<()> {
        self.0.init(names).map_err(|err| {
            StorageError::from_err("Failed to init Embedded DB".to_owned(), Box::new(err))
        })
    }

    fn save(&self, name: String, value: &Self::Value) -> StorageResult<Self::Key> {
        self.0.save(&name, value).map_err(|err| {
            StorageError::from_err("Failed to save to Embedded DB".to_owned(), Box::new(err))
        })
    }

    fn load(&self, name: String) -> StorageResult<Option<(Self::Key, Self::Value)>> {
        self.0.load(&name).map_err(|err| {
            StorageError::from_err("Failed to load from Embedded DB".to_owned(), Box::new(err))
        })
    }

    fn batch_load(
        &self,
        name: String,
        batch_size: usize,
    ) -> StorageResult<VecDeque<(Self::Key, Self::Value)>> {
        self.0.batch(&name, batch_size).map_err(|err| {
            StorageError::from_err(
                "Failed to batch load from Embedded DB".to_owned(),
                Box::new(err),
            )
        })
    }

    fn remove(&self, name: String, key: &Self::Key) -> StorageResult<()> {
        self.0.remove(&name, key).map_err(|err| {
            StorageError::from_err(
                "Failed to remove from Embedded DB".to_owned(),
                Box::new(err),
            )
        })
    }
}
