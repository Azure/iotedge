use std::cell::BorrowMutError;

use bincode::ErrorKind;
use serde::{Deserialize, Serialize};
use thiserror::Error;

mod loader;
mod publication_store;
mod waking_state;
pub use loader::MessageLoader;
pub use publication_store::PublicationStore;
pub use waking_state::{memory::WakingMemoryStore, StreamWakeableState};

/// Keys used in persistence.
/// Ordered by offset
#[derive(Hash, Eq, Ord, PartialOrd, PartialEq, Clone, Debug, Deserialize, Serialize, Copy)]
pub struct Key {
    offset: u32,
}

#[derive(Debug, Error)]
pub enum PersistError {
    #[error("Failed to deserialize database entry")]
    Deserialization(#[source] Box<ErrorKind>),

    #[error("Failed to get rocksdb column family")]
    GetColumnFamily,

    #[error("Attempted to remove entry which does not exist")]
    RemovalForMissing,

    #[error("Failed to serialize on database insert")]
    Serialization(#[source] Box<ErrorKind>),

    #[error("Failed to serialize on database insert")]
    BorrowSharedState(#[from] BorrowMutError),
}
