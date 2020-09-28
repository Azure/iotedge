use std::{error::Error, sync::Arc};

use async_trait::async_trait;
use futures_util::stream::Stream;
use mqtt3::proto::Publication;
use parking_lot::Mutex;

pub mod memory;

/// Persistence used in bridge.
/// Elements are added, then can be removed once retrieved by the loader
/// If one attempts to remove added elements before reading via the loader remove returns None
#[async_trait]
pub trait Persist<'a> {
    type Loader: Stream;
    type Error: Error;

    fn new(batch_size: usize) -> Self;

    async fn push(&mut self, message: Publication) -> Result<Key, Self::Error>;

    async fn remove(&mut self, key: Key) -> Option<Publication>;

    async fn loader(&'a mut self) -> Arc<Mutex<Self::Loader>>;
}

/// Keys used in persistence.
/// Ordered by offset
#[derive(Hash, Eq, Ord, PartialOrd, PartialEq, Clone, Debug)]
pub struct Key {
    offset: u32,
}

#[cfg(test)]
mod tests {
    use crate::persist::Key;

    #[test]
    fn key_offset_ordering() {
        // ordered by offset
        let key1 = Key { offset: 0 };
        let key2 = Key { offset: 1 };
        let key3 = Key { offset: 1 };
        assert!(key2 > key1);
        assert!(key2 != key1);
        assert!(key1 < key2);
        assert!(key2 == key3);
    }
}
