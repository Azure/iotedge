use std::io::Error as IOError;

#[derive(Debug, thiserror::Error)]
pub enum BlockError {
    #[error("Bad hint")]
    Hint,

    #[error("Unexpected data hash {found:?} expected {expected:?}")]
    DataHash { found: u64, expected: u64 },

    #[error("Unexpected block hash {found:?} expected {expected:?}")]
    BlockHash { found: u64, expected: u64 },

    #[error("Unexpected data size {found:?} expected {expected:?}")]
    DataSize { found: usize, expected: usize },
}

#[derive(Debug, thiserror::Error)]
pub enum RingBufferError {
    #[error("Failed to validate internal details {0}")]
    Validate(#[from] BlockError),

    #[error("Serialization error occurred {0}")]
    Serialization(#[from] bincode::Error),

    #[error("Flushing failed {0}")]
    Flush(#[from] IOError),

    #[error("Key is at invalid index for removal")]
    RemovalIndex,

    #[error("Key does not exist")]
    NonExistantKey,

    #[error("Buffer is full and messages must be drained to continue")]
    Full,

    #[error("Cannot remove before reading")]
    RemoveBeforeRead
}
