use std::error::Error as StdError;

use self::error::RingBufferError;

pub mod block;
pub mod error;
pub mod fixed_mmap;

fn to_ring_buffer_err(message: String, err: Box<dyn StdError>) -> RingBufferError {
    RingBufferError::new(message, Some(err))
}

pub type RingBufferResult<T> = Result<T, RingBufferError>;
