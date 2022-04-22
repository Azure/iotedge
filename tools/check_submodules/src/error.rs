// Copyright (c) Microsoft. All rights reserved.

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("Found submodule inconsistencies.")]
    Count(i64),
    #[error("Git library error")]
    Git,
}
