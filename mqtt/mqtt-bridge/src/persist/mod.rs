use bincode::ErrorKind;
use rocksdb::Error;
use serde::{Deserialize, Serialize};
use thiserror::Error;

pub mod loader;
pub mod persistor;
mod waking_state;
pub use waking_state::waking_map::WakingMap;
pub use waking_state::waking_store::WakingStore;

/// Keys used in persistence.
/// Ordered by offset
#[derive(Hash, Eq, Ord, PartialOrd, PartialEq, Clone, Debug, Deserialize, Serialize)]
pub struct Key {
    offset: u32,
}

#[derive(Debug, Error)]
pub enum PersistError {
    #[error("Failed to create rocksdb column family")]
    CreateColumnFamily(#[source] Error),

    #[error("Failed to deserialize database entry")]
    Deserialization(#[source] Box<ErrorKind>),

    #[error("Failed to get rocksdb column family")]
    GetColumnFamily(),

    #[error("Failed to serialize on database insert")]
    Insertion(#[source] Error),

    #[error("Failed to remove element from persistent store. Element either does not exist or is not in-flight.")]
    Removal(#[source] Error),

    #[error("Attempted to remove entry which does not exist")]
    RemovalForMissing(),

    #[error("Failed to serialize on database insert")]
    Serialization(#[source] Box<ErrorKind>),
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
