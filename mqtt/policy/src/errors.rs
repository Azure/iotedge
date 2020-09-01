use thiserror::Error;

/// A specialized `Result` type for mini-redis operations.
///
/// This is defined as a convenience.
pub type Result<T> = std::result::Result<T, Error>;

#[derive(Debug, Error)]
pub enum Error {
    #[error("An error occurred deserializing policy definition.")]
    Deserializing(#[source] serde_json::Error),

    #[error("An error occurred validating policy definition: {0:?}.")]
    ValidationSummary(Vec<Error>),

    #[error("{0}")]
    Validation(String),

    #[error("An error occurred constructing the request: {0}.")]
    BadRequest(String),
}
