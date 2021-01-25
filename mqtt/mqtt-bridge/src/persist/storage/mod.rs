use async_trait::async_trait;
use std::{
    collections::VecDeque,
    future::Future,
    pin::Pin,
    sync::atomic::{AtomicUsize, Ordering},
    task::Waker,
};

pub mod ring_buffer;
// pub mod sled;
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

#[derive(Clone, Copy, Debug)]
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

pub trait Storage<Key, Value>: Send + Sync {
    fn insert_future<'a>(
        &self,
        value: &Vec<u8>,
    ) -> Pin<Box<dyn Future<Output = StorageResult<()>> + Send + 'a>>;
    fn batch_future<'a>(
        &self,
        amount: usize,
    ) -> Pin<Box<dyn Future<Output = StorageResult<VecDeque<(Key, Value)>>> + Send + 'a>>;
    fn remove_future<'a>(
        &self,
        key: &Key,
    ) -> Pin<Box<dyn Future<Output = StorageResult<()>> + Send + 'a>>;
    fn set_waker(&mut self, waker: Waker);
    fn waker(&mut self) -> &mut Option<Waker>;
}

#[async_trait]
impl<T> StreamWakeableState for T
where
    T: Storage<usize, Vec<u8>> + Send + Sync,
{
    async fn insert(&self, _key: Key, value: Publication) -> Result<(), PersistError> {
        let data = binary_serialize(&value)
            .map_err(StorageError::Serialization)
            .map_err(PersistError::Storage)?;
        self.insert_future(&data)
            .await
            .map_err(PersistError::Storage)
    }

    async fn batch(&self, count: usize) -> Result<VecDeque<(Key, Publication)>, PersistError> {
        println!("await batch");
        let batch = self
            .batch_future(count)
            .await
            .map_err(PersistError::Storage)?;
        let mut results = VecDeque::new();
        println!("translate batch");
        for (key, value) in batch {
            let key_wrapper = Key { offset: key as u64 };
            let publication = binary_deserialize::<Publication>(&value)
                .map_err(StorageError::Serialization)
                .map_err(PersistError::Storage)?;
            results.push_back((key_wrapper, publication));
        }

        Ok(results)
    }

    async fn remove(&self, key: Key) -> Result<(), PersistError> {
        let real_key = key.offset as usize;
        self.remove_future(&real_key)
            .await
            .map_err(PersistError::Storage)
    }

    fn set_waker(&mut self, waker: &Waker) {
        self.set_waker(waker.clone())
    }
}
