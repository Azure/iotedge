use serde::{Deserialize, Serialize};

mod loader;
mod publication_store;
mod storage;
mod waking_state;
pub use loader::MessageLoader;
pub use publication_store::PublicationStore;
pub use waking_state::{memory::WakingMemoryStore, StreamWakeableState};
use storage::error::StorageError;

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
    #[error("Underlying storage error occurred: {0}")]
    Storage(StorageError)
}
