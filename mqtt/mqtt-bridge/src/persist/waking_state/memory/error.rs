use thiserror::Error;

#[derive(Debug, Error)]
pub enum MemoryError {
    #[error("Cannot remove key that is not in order")]
    BadKeyOrdering,

    #[error("In memory buffer is full")]
    Full,

    #[error("Cannot remove when no keys are present")]
    RemoveOnEmpty,
}
