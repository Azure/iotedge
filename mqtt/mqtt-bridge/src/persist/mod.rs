use bincode::ErrorKind;
use rocksdb::Error;
use serde::{Deserialize, Serialize};
use thiserror::Error;

mod loader;
mod persistor;
mod waking_state;
pub use loader::MessageLoader;
pub use persistor::PublicationStore;
pub use waking_state::{
    memory::WakingMemoryStore, rocksdb::WakingRocksDBStore, StreamWakeableState,
};

/// Keys used in persistence.
/// Ordered by offset
#[derive(Hash, Eq, Ord, PartialOrd, PartialEq, Clone, Debug, Deserialize, Serialize, Copy)]
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
    GetColumnFamily,

    #[error("Failed to serialize on database insert")]
    Insertion(#[source] Error),

    #[error("Failed to remove element from persistent store. Element either does not exist or is not yet loaded.")]
    Removal(#[source] Error),

    #[error("Attempted to remove entry which does not exist")]
    RemovalForMissing,

    #[error("Failed to serialize on database insert")]
    Serialization(#[source] Box<ErrorKind>),
}
