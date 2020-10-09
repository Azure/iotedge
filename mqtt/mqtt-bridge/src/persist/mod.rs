use std::cell::BorrowMutError;

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
    #[error("Attempted to remove entry which does not exist")]
    RemovalForMissing,

    #[error("Failed to borrow shared state for persistence")]
    BorrowSharedState(#[from] BorrowMutError),
}
