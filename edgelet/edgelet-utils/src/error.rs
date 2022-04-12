// Copyright (c) Microsoft. All rights reserved.

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("Invalid argument - [{0}]")]
    Argument(String),

    #[error("Argument {0} out of range [{1}, {2}) ")]
    ArgumentOutOfRange(String, String, String),

    #[error("Argument {0} should be greater than {1}")]
    ArgumentTooLow(String, String),

    #[error("Argument is empty or only has whitespace - [{0}]")]
    ArgumentEmpty(String),

    #[error("Could not clone value via serde")]
    SerdeClone(#[from] serde_json::Error),
}
