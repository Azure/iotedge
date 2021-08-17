// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};

use edgelet_core::{
    ModuleOperation, ModuleRuntimeErrorReason, RegistryOperation, RuntimeOperation,
};

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Could not clone create options")]
    CloneCreateOptions,

    #[fail(display = "Conflict with current operation")]
    Conflict,

    #[fail(display = "Container runtime error")]
    Docker,

    #[fail(display = "Container runtime error - {:?}", _0)]
    DockerRuntime(String),

    #[fail(display = "{}", _0)]
    FormattedDockerRuntime(String),

    #[fail(display = "Could not initialize module runtime")]
    Initialization,

    #[fail(display = "Could not initialize Notary configuration: {}", _0)]
    InitializeNotary(String),

    #[fail(display = "Invalid docker image {:?}", _0)]
    InvalidImage(String),

    #[fail(display = "Invalid module name {:?}", _0)]
    InvalidModuleName(String),

    #[fail(display = "Invalid module type {:?}", _0)]
    InvalidModuleType(String),

    #[fail(display = "Invalid socket URI: {:?}", _0)]
    InvalidSocketUri(String),

    #[fail(display = "{}", _0)]
    LaunchNotary(String),

    #[fail(display = "{}", _0)]
    ModuleOperation(ModuleOperation),

    #[fail(display = "{}", _0)]
    NotaryDigestMismatch(String),

    #[fail(display = "{}", _0)]
    NotaryRootCAReadError(String),

    #[fail(display = "{}", _0)]
    NotFound(String),

    #[fail(display = "Target of operation already in this state")]
    NotModified,

    #[fail(display = "{}", _0)]
    RegistryOperation(RegistryOperation),

    #[fail(display = "{}", _0)]
    RuntimeOperation(RuntimeOperation),
}

impl Fail for Error {
    fn cause(&self) -> Option<&dyn Fail> {
        self.inner.cause()
    }

    fn backtrace(&self) -> Option<&Backtrace> {
        self.inner.backtrace()
    }
}

impl Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        Display::fmt(&self.inner, f)
    }
}

impl Error {
    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
    }

    pub fn from_docker_error(err: Box<dyn std::error::Error>, context: ErrorKind) -> Self {
        Error::from(ErrorKind::DockerRuntime(err.to_string())).context(context).into()
    }
}

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Self {
        Error {
            inner: Context::new(kind),
        }
    }
}

impl From<Context<ErrorKind>> for Error {
    fn from(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }
}

impl<'a> From<&'a Error> for ModuleRuntimeErrorReason {
    fn from(err: &'a Error) -> Self {
        match <dyn Fail>::find_root_cause(err).downcast_ref::<ErrorKind>() {
            Some(ErrorKind::NotFound(_)) => ModuleRuntimeErrorReason::NotFound,
            _ => ModuleRuntimeErrorReason::Other,
        }
    }
}
