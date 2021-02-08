mod loader;
mod publication_store;
mod waking_state;

use bincode::Error as BincodeError;
use serde::{Deserialize, Serialize};

pub use loader::MessageLoader;
pub use publication_store::PublicationStore;
use waking_state::ring_buffer::error::RingBufferError;
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
    #[error("RingBuffer error occurred. Caused by: {0}")]
    RingBuffer(#[from] RingBufferError),

    #[error("Serialization error occurred {0}")]
    Serialization(#[from] BincodeError),
}
