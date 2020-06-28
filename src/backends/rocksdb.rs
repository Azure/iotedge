use crate::store::{Record, StoreBackend};

use std::error::Error as StdError;
use std::fmt::{Display, Formatter, Result as FormatResult};
use std::path::Path;
use std::string::FromUtf8Error;

use rocksdb::{DB, Error as EngineError, Options};
use serde_json::{from_str, to_string, Error as SerdeError};

pub struct RocksDBBackend(DB);
#[derive(Debug)]
pub enum RocksDBError {
    Engine(EngineError),
    RawData(FromUtf8Error),
    Serialization(SerdeError),
    NotFound(String)
}

impl StoreBackend for RocksDBBackend {
    type Error = RocksDBError;

    fn new() -> Result<Self, Self::Error> {
        let mut opts = Options::default();
        opts.create_if_missing(true);

        let db = DB::open(&opts, Path::new("store.rdb"))?;

        Ok(RocksDBBackend(db))
    }

    fn write_record(&self, id: &str, record: Record) -> Result<(), Self::Error> {
        let ser = match to_string(&record) {
            Ok(val) => val,
            _ => panic!("FAILED TO SERIALIZE RECORD")
        };
        self.0.put(id, ser)?;
        Ok(())
    }

    fn update_record(&self, id: &str, record: Record) -> Result<(), Self::Error> {
        self.write_record(id, record)
    }

    fn read_record(&self, id: &str) -> Result<Record, Self::Error> {
        match self.0.get(id)? {
            Some(val) => Ok(from_str(&String::from_utf8(val)?)?),
            None => Err(RocksDBError::NotFound(id.to_string()))
        }
    }

    fn delete_record(&self, id: &str) -> Result<(), Self::Error> {
        self.0.delete(id)?;
        Ok(())
    }
}

impl Display for RocksDBError {
    fn fmt(&self, f: &mut Formatter<'_>) -> FormatResult {
        match self {
            RocksDBError::Engine(e) => e.fmt(f),
            RocksDBError::RawData(e) => e.fmt(f),
            RocksDBError::Serialization(e) => e.fmt(f),
            RocksDBError::NotFound(id) => write!(f, "NO SUCH ID: {}", id)
        }
    }
}

impl From<EngineError> for RocksDBError {
    fn from(eng: EngineError) -> Self {
        RocksDBError::Engine(eng)
    }
}

impl From<SerdeError> for RocksDBError {
    fn from(ser: SerdeError) -> Self {
        RocksDBError::Serialization(ser)
    }
}

impl From<FromUtf8Error> for RocksDBError {
    fn from(utf8: FromUtf8Error) -> Self {
        RocksDBError::RawData(utf8)
    }
}

impl StdError for RocksDBError { }
