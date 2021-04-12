use crate::persist::Key;

#[derive(Debug, thiserror::Error)]
pub enum BlockError {
    #[error("Unexpected block crc {found} expected {expected}")]
    BlockCrc { found: u32, expected: u32 },

    #[error("Failed to create block. Caused by {0}")]
    BlockCreation(#[from] bincode::Error),

    #[error("Unexpected data crc {found} expected {expected}")]
    DataCrc { found: u32, expected: u32 },

    #[error("Unexpected data size {found} expected {expected}")]
    DataSize { found: u64, expected: u64 },

    #[error("Bad hint")]
    Hint,
}

#[derive(Debug, thiserror::Error)]
pub enum RingBufferError {
    #[error("Underlying block error occurred. Caused by {0}")]
    Block(BlockError),

    #[error("Flushing failed. Caused by {0}")]
    Flush(std::io::Error),

    #[error("Storage has insufficient space to insert data: required: {required}b, but only {free}b available")]
    InsufficientSpace { free: u64, required: u64 },

    #[error("Unable to create file. Caused by {0}")]
    FileCreate(std::io::Error),

    #[error("File IO error occurred. Caused by {0}")]
    FileIo(std::io::Error),

    #[error("Storage file metadata unavailable. Caused by {0}")]
    FileMetadata(std::io::Error),

    #[error(
        "Storage file cannot be truncated. Caused by new max size {new} being less than {current}"
    )]
    FileTruncation { current: u64, new: u64 },

    #[error("Read unknown block with {current} but {expected} hint expected")]
    UnknownBlock { current: u32, expected: u32 },

    #[error("Cannot remove before reading a publication with key {0}")]
    RemoveBeforeRead(Key),

    #[error("Serialization error occurred. Caused by {0}")]
    Serialization(#[from] bincode::Error),

    #[error("Failed to validate internal details. Caused by {0}")]
    Validate(BlockError),
}
