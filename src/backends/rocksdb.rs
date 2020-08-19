use crate::constants::STATE_DIRECTORY;
use crate::store::{Record, StoreBackend};

use std::path::Path;
use std::string::FromUtf8Error;

use failure::{Fail, ResultExt};
use rocksdb::{DB, Error as EngineError, Options};
use serde_json::{from_str, to_string, Error as SerdeError};

const STORE_NAME: &str = "db.rocks";

pub struct RocksDBBackend(DB);
#[derive(Copy, Clone, Debug, Fail, PartialEq)]
pub enum RocksDBError {
    #[fail(display = "Deserialization failure")]
    Deserialization,
    #[fail(display = "RocksDB engine error")]
    Engine,
    #[fail(display = "Initialization error")]
    Initialization,
    #[fail(display = "UTF8 parsing error")]
    RawData,
    #[fail(display = "Serialization failure")]
    Serialization,
    #[fail(display = "No results")]
    NotFound
}

impl RocksDBBackend {
    pub fn new() -> Result<Self, <Self as StoreBackend>::Error> {
        let mut opts = Options::default();
        opts.create_if_missing(true);

        let db = DB::open(&opts, Path::new(format!("{}/{}", STATE_DIRECTORY, STORE_NAME)))
            .map_err(|_| RocksDBError::Initialization)?;

        Ok(RocksDBBackend(db))
    }
}

impl StoreBackend for RocksDBBackend {
    type Error = RocksDBError;

    fn init(&self) -> Result<(), Self::Error> {
        Ok(())
    }

    fn write_record(&self, id: &str, record: Record) -> Result<(), Self::Error> {
        let ser = to_string(&record)
            .map_err(|_| RocksDBError::Deserialization)?;
        self.0.put(id, ser)
            .map_err(|_| RocksDBError::Engine)?;
        Ok(())
    }

    fn update_record(&self, id: &str, record: Record) -> Result<(), Self::Error> {
        self.write_record(id, record)
    }

    fn read_record(&self, id: &str) -> Result<Record, Self::Error> {
        match self.0.get(id).map_err(|_| RocksDBError::Engine)? {
            Some(val) => {
                let val_string = String::from_utf8(val)
                    .map_err(|_| RocksDBError::RawData)?;
                let record = from_str(&val_string)
                    .map_err(|_| RocksDBError::Deserialization)?;
                Ok(record)
            },
            None => Err(RocksDBError::NotFound)
        }
    }

    fn delete_record(&self, id: &str) -> Result<(), Self::Error> {
        self.0.delete(id)
            .map_err(|_| RocksDBError::Engine)?;
        Ok(())
    }
}
