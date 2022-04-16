// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{ModuleOperation, RegistryOperation, RuntimeOperation};

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("Could not clone create options")]
    CloneCreateOptions,

    #[error("Conflict with current operation")]
    Conflict,

    #[error("Container runtime error")]
    Docker,

    #[error("Container runtime error - {0}")]
    DockerRuntime(String),

    #[error("{0}")]
    FormattedDockerRuntime(String),

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
    NotFound(String),

    #[error("Target of operation already in this state")]
    NotModified,

    #[error("{0}")]
    RegistryOperation(RegistryOperation),

    #[error("{0}")]
    RuntimeOperation(RuntimeOperation),
}
