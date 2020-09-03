use crate::store::{Record, StoreBackend};

use std::path::Path;

use failure::Fail;
use rocksdb::{DB, Options};
use serde_json::{from_str, to_string};

const STORE_NAME: &str = "store.rdb";

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
    Serialization
}

impl RocksDBBackend {
    pub fn new(path: &str) -> Result<Self, <Self as StoreBackend>::Error> {
        let mut opts = Options::default();
        opts.create_if_missing(true);

        let db = DB::open(&opts, Path::new(&format!("{}/{}", path, STORE_NAME)))
            .map_err(|_| RocksDBError::Initialization)?;

        let backend = Self(db);
        backend.init()
            .map_err(|_| RocksDBError::Initialization)?;

        Ok(backend)
    }
}

impl StoreBackend for RocksDBBackend {
    type Error = RocksDBError;

    fn init(&self) -> Result<(), Self::Error> {
        Ok(())
    }

    fn write_record(&self, id: &str, record: Record) -> Result<(), Self::Error> {
        let ser = to_string(&record)
            .map_err(|_| RocksDBError::Serialization)?;
        self.0.put(id, ser)
            .map_err(|_| RocksDBError::Engine)?;
        Ok(())
    }

    fn read_record(&self, id: &str) -> Result<Option<Record>, Self::Error> {
        self.0.get(id)
            .map_err(|_| RocksDBError::Engine)?
            .map(|val| {
                let val_string = String::from_utf8(val)
                    .map_err(|_| RocksDBError::RawData)?;
                let record = from_str(&val_string)
                    .map_err(|_| RocksDBError::Deserialization)?;
                Ok(record)
            })
            .transpose()
    }

    fn delete_record(&self, id: &str) -> Result<(), Self::Error> {
        self.0.delete(id)
            .map_err(|_| RocksDBError::Engine)?;
        Ok(())
    }
}
