use async_trait::async_trait;
use futures_util::stream::Stream;
use mqtt3::proto::Publication;
use thiserror::Error;

mod memory;

// Persistence used in bridge.
#[async_trait]
trait Persist<'a> {
    type Loader: Stream;

    fn new() -> Self;

    async fn insert(&mut self, message: Publication) -> Result<Key, PersistError>;

    async fn remove(&mut self, key: Key) -> Result<bool, PersistError>;

    async fn loader(&'a mut self, batch_size: usize) -> Self::Loader;
}

// Keys used in persistence.
// Ordered by offset
#[derive(Eq, Ord, PartialOrd, PartialEq, Clone, Debug)]
pub struct Key {
    offset: u32,
}

#[derive(Debug, Error)]
pub enum PersistError {
    #[error("Failed to remove messages from persistence")]
    Removal(),
}

#[cfg(test)]
mod tests {
    use crate::persist::Key;

    #[test]
    fn key_offset_ordering() {
        // ordered by offset
        let key1 = Key { offset: 0 };
        let key2 = Key { offset: 1 };
        assert!(key2 > key1);
        assert!(key2 != key1);
        assert!(key1 < key2);
    }
}
