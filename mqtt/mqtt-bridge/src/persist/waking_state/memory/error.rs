use thiserror::Error;

use crate::persist::Key;

#[derive(Debug, Error)]
pub enum MemoryError {
    #[error("In memory buffer is full")]
    Full,

    #[error("Cannot remove when no keys are present")]
    RemoveOnEmpty,

    #[error("Cannot remove before reading a publication with key {0}")]
    RemoveBeforeRead(Key),
}
