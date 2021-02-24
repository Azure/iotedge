mod loader;
mod publication_store;
mod waking_state;

use bincode::Error as BincodeError;
use serde::{Deserialize, Serialize};

pub use loader::MessageLoader;
pub use publication_store::PublicationStore;
use waking_state::memory::error::MemoryError;
pub use waking_state::{
    memory::WakingMemoryStore,
    ring_buffer::{error::RingBufferError, flush::FlushOptions, RingBuffer},
    StreamWakeableState,
};

/// Keys used in persistence.
/// Ordered by offset
#[derive(Hash, Eq, Ord, PartialOrd, PartialEq, Clone, Debug, Deserialize, Serialize, Copy)]
pub struct Key {
    offset: u64,
}

#[derive(Debug, thiserror::Error)]
pub enum PersistError {
    #[error("RingBuffer error occurred. Caused by: {0}")]
    RingBuffer(#[from] RingBufferError),

    #[error("Attempted to remove entry which does not exist")]
    RemovalForMissing,

    #[error("Memory error occurred. Caused by: {0}")]
    Memory(#[from] MemoryError),

    #[error("Serialization error occurred. Caused by: {0}")]
    Serialization(#[from] BincodeError),
}

pub type PersistResult<T> = Result<T, PersistError>;
