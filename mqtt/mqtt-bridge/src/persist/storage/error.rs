use crate::persist::storage::ring_buffer::error::RingBufferError;

use bincode::Error as BincodeError;
use sled::Error as SledError;
use thiserror::Error;

// create_error!(StorageError);


#[derive(Debug, Error)]
pub enum StorageError {
    #[error("RingBuffer error occurred")]
    RingBuffer(#[from] RingBufferError),
    #[error("Database error occurred")]
    Database(#[from] SledError),
    #[error("Serialization error occurred")]
    Serialization(#[from] BincodeError)
}

// impl From<RingBufferError> for StorageError {
//     fn from(err: RingBufferError) -> Self {
//         Self::from_err(Box::new(err))
//     }
// }

// impl From<SledError> for StorageError {
//     fn from(err: SledError) -> Self {
//         Self::from_err(Box::new(err))
//     }
// }

// impl From<BincodeError> for StorageError {
//     fn from(err: BincodeError) -> Self {
//         Self::from_err(Box::new(err))
//     }
// }
