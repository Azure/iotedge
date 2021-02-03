use thiserror::Error;

#[derive(Debug, Error)]
pub enum MemoryError {
    #[error("Bad key for removal")]
    BadKey,
}
