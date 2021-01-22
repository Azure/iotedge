use std::{
    collections::VecDeque,
    sync::atomic::{AtomicUsize, Ordering},
    task::Waker,
};

pub mod ring_buffer;
pub mod sled;
#[macro_use]
mod error_macro;
pub mod error;
mod serialize;

use error::StorageError;
use mqtt3::proto::Publication;
use serialize::binary_deserialize;

use self::serialize::binary_serialize;

use super::{Key, PersistError, StreamWakeableState};

pub type StorageResult<T> = Result<T, StorageError>;

pub enum IOStatus<T> {
    Pending,
    Ready(T),
}

pub enum FlushOptions {
    AfterEachWrite,
    AfterXWrites(usize),
    AfterXBytes(usize),
    AfterXMilliseconds(usize),
    Off,
}

pub struct FlushState {
    writes: AtomicUsize,
    bytes_written: AtomicUsize,
    millis_elapsed: AtomicUsize,
}

impl FlushState {
    fn new() -> Self {
        Self {
            writes: AtomicUsize::default(),
            bytes_written: AtomicUsize::default(),
            millis_elapsed: AtomicUsize::default(),
        }
    }

    fn reset(&self, flush_option: &FlushOptions) {
        match flush_option {
            FlushOptions::AfterEachWrite => {}
            FlushOptions::AfterXWrites(_) => {
                self.writes.store(0, Ordering::SeqCst);
            }
            FlushOptions::AfterXBytes(_) => {
                self.bytes_written.store(0, Ordering::SeqCst);
            }
            FlushOptions::AfterXMilliseconds(_) => {
                self.millis_elapsed.store(0, Ordering::SeqCst);
            }
            FlushOptions::Off => {}
        }
    }

    fn update(&self, writes: usize, bytes_written: usize, millis_elapsed: usize) {
        self.bytes_written
            .fetch_add(bytes_written, Ordering::SeqCst);
        self.millis_elapsed
            .fetch_add(millis_elapsed, Ordering::SeqCst);
        self.writes.fetch_add(writes, Ordering::SeqCst);
    }
}

pub trait Storage<Key, Value> {
    fn insert(&self, value: &Value) -> StorageResult<IOStatus<()>>;
    fn batch(&self, amount: usize) -> StorageResult<IOStatus<VecDeque<(Key, Value)>>>;
    fn remove(&self, key: &Key) -> StorageResult<IOStatus<()>>;
    fn set_waker(&mut self, waker: Waker);
}

impl<T> StreamWakeableState for T
where
    T: Storage<usize, Vec<u8>>,
{
    fn insert(&mut self, _key: Key, value: Publication) -> Result<(), super::PersistError> {
        let me = self as &mut dyn Storage<usize, Vec<u8>>;
        let data = binary_serialize(&value)
            .map_err(StorageError::Serialization)
            .map_err(PersistError::Storage)?;
        me.insert(&data).map(|_| ()).map_err(PersistError::Storage)
    }

    fn batch(&mut self, count: usize) -> Result<VecDeque<(Key, Publication)>, super::PersistError> {
        let me = self as &mut dyn Storage<usize, Vec<u8>>;
        let results = match me.batch(count).map_err(PersistError::Storage)? {
            IOStatus::Ready(batch) => {
                let mut results = VecDeque::new();
                for (key, value) in batch {
                    let key_wrapper = Key {
                        offset: key as u64,
                    };
                    let publication = binary_deserialize::<Publication>(&value)
                        .map_err(StorageError::Serialization)
                        .map_err(PersistError::Storage)?;
                    results.push_back((key_wrapper, publication));
                }
                results
            }
            IOStatus::Pending => VecDeque::new(),
        };
        Ok(results)
    }

    fn remove(&mut self, key: Key) -> Result<(), super::PersistError> {
        let me = self as &mut dyn Storage<usize, Vec<u8>>;
        let real_key = key.offset as usize;
        me.remove(&real_key)
            .map(|_| ())
            .map_err(PersistError::Storage)
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.set_waker(waker.clone())
    }
}
