// Copyright (c) Microsoft. All rights reserved.

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("Found submodule inconsistencies.")]
    Count(i64),
    #[error("Git library error")]
    Git,
}

impl From<i64> for Error {
    fn from(value: i64) -> Self {
        Error::Count(value)
    }
}
