// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Debug, thiserror::Error)]
pub enum Error {
    #[error("A module runtime error occurred")]
    ModuleRuntime,

    #[error("Could not generate support bundle")]
    SupportBundle,

    #[error("Could not write")]
    Write,
}
