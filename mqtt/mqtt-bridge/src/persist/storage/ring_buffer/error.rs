// use crate::create_error;

// create_error!(RingBufferError);
use std::io::Error as IOError;
use thiserror::Error;

#[derive(Debug, Error)]
pub enum BlockError {
    #[error("Unexpected data hash {found:?} expected {expected:?}")]
    DataHash { found: u64, expected: u64 },
    #[error("Unexpected block hash {found:?} expected {expected:?}")]
    BlockHash { found: u64, expected: u64 },
    #[error("Unexpected data size {found:?} expected {expected:?}")]
    DataSize { found: usize, expected: usize },
}

#[derive(Debug, Error)]
pub enum RingBufferError {
    #[error("Failed to validate internal details")]
    Validate(#[from] BlockError),
    #[error("Unable to fit data and possible corruption")]
    WrapAround,
    #[error("Serialization error occurred")]
    Serialization(#[from] bincode::Error),
    #[error("Flushing failed")]
    Flush(#[from] IOError),
}
