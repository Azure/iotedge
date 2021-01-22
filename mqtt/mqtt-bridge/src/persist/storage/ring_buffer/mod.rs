mod block;
pub mod error;
mod mmap;
pub mod ring_buffer;

pub type RingBufferResult<T> = Result<T, error::RingBufferError>;
