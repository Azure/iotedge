mod loader;
mod publication_store;
mod storage;
mod waking_state;

use self::storage::ring_buffer::error::RingBufferError;
use bincode::Error as BincodeError;
pub use loader::MessageLoader;
pub use publication_store::PublicationStore;
use serde::{Deserialize, Serialize};
pub use waking_state::{memory::WakingMemoryStore, StreamWakeableState};

/// Keys used in persistence.
/// Ordered by offset
#[derive(Hash, Eq, Ord, PartialOrd, PartialEq, Clone, Debug, Deserialize, Serialize, Copy)]
pub struct Key {
    offset: u64,
}

#[derive(Debug, thiserror::Error)]
pub enum PersistError {
    #[error("Attempted to remove entry which does not exist")]
    RemovalForMissing,
}

// This might replace the PersistError or merge with it.
#[derive(Debug, thiserror::Error)]
pub enum StorageError {
    #[error("RingBuffer error occurred")]
    RingBuffer(#[from] RingBufferError),
    #[error("Serialization error occurred")]
    Serialization(#[from] BincodeError),
}
