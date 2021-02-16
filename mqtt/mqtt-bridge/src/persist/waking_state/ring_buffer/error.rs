#[derive(Debug, thiserror::Error)]
pub enum BlockError {
    #[error("Unexpected block hash {found:?} expected {expected:?}")]
    BlockHash { found: u64, expected: u64 },

    #[error("Unexpected data hash {found:?} expected {expected:?}")]
    DataHash { found: u64, expected: u64 },

    #[error("Unexpected data size {found:?} expected {expected:?}")]
    DataSize { found: u64, expected: u64 },

    #[error("Bad hint")]
    Hint,
}

#[derive(Debug, thiserror::Error)]
pub enum RingBufferError {
    #[error("Flushing failed. Caused by {0}")]
    Flush(std::io::Error),

    #[error("Buffer is full and messages must be drained to continue")]
    Full,

    #[error("Unable to create file. Caused by {0}")]
    FileCreate(std::io::Error),

    #[error("File IO error occurred. Caused by {0}")]
    FileIO(std::io::Error),

    #[error("File metadata unavailable")]
    FileMetadata(std::io::Error),

    #[error("File cannot be truncated.")]
    FileTruncation,

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
