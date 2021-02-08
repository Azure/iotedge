#[derive(Debug, thiserror::Error)]
pub enum BlockError {
    #[error("Unexpected block hash {found:?} expected {expected:?}")]
    BlockHash { found: u64, expected: u64 },

    #[error("Unexpected data hash {found:?} expected {expected:?}")]
    DataHash { found: u64, expected: u64 },

    #[error("Unexpected data size {found:?} expected {expected:?}")]
    DataSize { found: usize, expected: usize },

    #[error("Bad hint")]
    Hint,
}

#[derive(Debug, thiserror::Error)]
pub enum RingBufferError {
    #[error("Flushing failed. Caused by {0}")]
    Flush(std::io::Error),

    #[error("Buffer is full and messages must be drained to continue")]
    Full,

    #[error("Mmap creation error occurred. Caused by {0}")]
    MmapCreate(std::io::Error),

    #[error("Key does not exist")]
    NonExistantKey,

    #[error("Key is at invalid index for removal")]
    RemovalIndex,

    #[error("Cannot remove before reading")]
    RemoveBeforeRead,

    #[error("Serialization error occurred. Caused by {0}")]
    Serialization(#[from] bincode::Error),

    #[error("Failed to validate internal details. Caused by {0}")]
    Validate(#[from] BlockError),
}
