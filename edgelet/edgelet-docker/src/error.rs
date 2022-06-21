// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{ModuleOperation, RegistryOperation, RuntimeOperation};

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("Container runtime error")]
    Docker,

    #[error("Could not initialize module runtime - {0}")]
    Initialization(String),

    #[error("Invalid docker image {0:?}")]
    InvalidImage(String),

    #[error("Invalid module name {0:?}")]
    InvalidModuleName(String),

    #[error("Invalid module type {0:?}")]
    InvalidModuleType(String),

    #[error("Invalid socket URI: {0:?}")]
    InvalidSocketUri(String),

    #[error("{0}")]
    ModuleOperation(ModuleOperation),

    #[error("{0}")]
    RegistryOperation(RegistryOperation),

    #[error("{0}")]
    RuntimeOperation(RuntimeOperation),
}
