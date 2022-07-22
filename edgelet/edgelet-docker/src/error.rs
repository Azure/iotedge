// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{ModuleOperation, RegistryOperation, RuntimeOperation};

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("container runtime error")]
    Docker,

    #[error("initialization failure")]
    Initialization,

    #[error("invalid module name: {0:?}")]
    InvalidModuleName(String),

    #[error("invalid module type: {0:?}")]
    InvalidModuleType(String),

    #[error("module operation error: {0}")]
    ModuleOperation(ModuleOperation),

    #[error("registry operation error: {0}")]
    RegistryOperation(RegistryOperation),

    #[error("runtime operation error: {0}")]
    RuntimeOperation(RuntimeOperation),

    #[error("")]
    Dummy(),
}
