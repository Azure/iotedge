use thiserror::Error;

#[derive(Debug, Error)]
pub enum MemoryError {
    #[error("Key does not exist")]
    NonExistantKey,
}
