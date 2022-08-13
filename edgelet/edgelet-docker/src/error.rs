// Copyright (c) Microsoft. All rights reserved.

use std::{io, num::ParseIntError, time::SystemTimeError};

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

    #[error("file operation error: {0}")]
    FileOperation(String),

    #[error("failed to calculate current time epoch: {0}")]
    GetCurrentTimeEpoch(SystemTimeError),

    #[error("attempted to get image hash but was nonexistent.")]
    GetImageId(),

    #[error("failed to lock for module image garbage collection: {0}")]
    LockError(String),

    #[error("failed to parse int: {0}")]
    ParseIntError(ParseIntError),

    #[error("failed to create file: {0}")]
    CreateFile(io::Error),

    #[error("failed to create filepath: {0}")]
    FilepathCreationError(String),
}
