use std::{error::Error as StdError};

use async_trait::async_trait;

use self::error::RingBufferError;

pub mod block;
pub mod error;
pub mod fixed_mmap;

fn to_ring_buffer_err(message: String, err: Box<dyn StdError>) -> RingBufferError {
    RingBufferError::new(message, Some(err))
}

pub type RingBufferResult<T> = Result<T, RingBufferError>;

#[async_trait]
pub trait RingBuffer {
    async fn enqueue(&mut self, data: &[u8]) -> RingBufferResult<()>;
    async fn dequeue(&mut self, data: &mut [u8]) -> RingBufferResult<()>;
}
