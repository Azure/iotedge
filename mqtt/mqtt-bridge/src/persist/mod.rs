use bincode::ErrorKind;
use rocksdb::Error;
use serde::{Deserialize, Serialize};
use thiserror::Error;

pub mod loader;
pub mod persistor;
mod waking_state;

/// Keys used in persistence.
/// Ordered by offset
#[derive(Hash, Eq, Ord, PartialOrd, PartialEq, Clone, Debug, Deserialize, Serialize)]
pub struct Key {
    offset: u32,
}

#[derive(Debug, Error)]
pub enum PersistError {
    #[error("Failed to serialize on database insert")]
    Serialization(#[from] Box<ErrorKind>),

    #[error("Failed to serialize on database insert")]
    Insertion(#[from] Error),
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
