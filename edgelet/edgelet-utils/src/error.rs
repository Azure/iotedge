// Copyright (c) Microsoft. All rights reserved.

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("invalid argument: {0:?}")]
    Argument(String),

    #[error("argument {0:?} out of range [{1}, {2})")]
    ArgumentOutOfRange(String, String, String),

    #[error("argument {0:?} should be greater than {1}")]
    ArgumentTooLow(String, String),

    #[error("an argument is empty or only has whitespace")]
    ArgumentEmpty,
}
