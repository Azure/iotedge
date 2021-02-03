mod loader;
mod publication_store;
pub mod waking_state;

use bincode::Error as BincodeError;
use serde::{Deserialize, Serialize};

pub use loader::MessageLoader;
pub use publication_store::PublicationStore;
pub use waking_state::{
    memory::error::MemoryError, memory::WakingMemoryStore, ring_buffer::error::RingBufferError,
};

/// Keys used in persistence.
/// Ordered by offset
#[derive(Hash, Eq, Ord, PartialOrd, PartialEq, Clone, Debug, Deserialize, Serialize, Copy)]
pub struct Key {
    offset: u64,
}

#[derive(Debug, thiserror::Error)]
pub enum StorageError {
    #[error("RingBuffer error occurred {0}")]
    RingBuffer(#[from] RingBufferError),

    #[error("Serialization error occurred {0}")]
    Serialization(#[from] BincodeError),

    #[error("Memory error occurred")]
    Memory(#[from] MemoryError),
}
