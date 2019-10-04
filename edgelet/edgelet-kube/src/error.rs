// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};

use edgelet_core::{ModuleRuntimeErrorReason, RuntimeOperation};

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "Could not initialize kubernetes module runtime")]
    Initialization,

    #[fail(display = "Invalid module name {:?}", _0)]
    InvalidModuleName(String),

    #[fail(display = "Invalid DNS name {:?}", _0)]
    InvalidDnsName(String),

    #[fail(display = "Device Id was not found")]
    MissingDeviceId,

    #[fail(display = "IoT Hub name was not found")]
    MissingHubName,

    #[fail(display = "Container not found in module, name = {:?}", _0)]
    ModuleNotFound(String),

    #[fail(display = "Image not found in PodSpec")]
    ImageNotFound,

    #[fail(display = "Could not execute runtime operation: {}", _0)]
    RuntimeOperation(RuntimeOperation),

    #[fail(display = "Could not execute registry operation")]
    RegistryOperation,

    #[fail(display = "Invalid authentication token")]
    InvalidAuthToken,

    #[fail(display = "Authentication failed")]
    Authentication,

    #[fail(display = "Pull image validation error: {}", _0)]
    PullImage(PullImageErrorReason),

    #[fail(display = "Kubernetes client error")]
    KubeClient,

    #[fail(display = "Could not convert pod definition to kubernetes module")]
    PodToModule,

    #[fail(display = "{}", _0)]
    NotFound(String),

    #[fail(display = "Config parsing error")]
    Config,

    #[fail(display = "An error occurred obtaining the client identity certificate")]
    IdentityCertificate,
}

#[derive(Clone, Debug, PartialEq)]
pub enum PullImageErrorReason {
    AuthName,
    AuthServerAddress,
    AuthUser,
    AuthPassword,
    Json,
}

impl Display for PullImageErrorReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::AuthName => write!(f, "Auth name not valid"),
            Self::AuthServerAddress => write!(f, "Auth server address not present"),
            Self::AuthUser => write!(f, "Auth user name not present"),
            Self::AuthPassword => write!(f, "Auth password not present"),
            Self::Json => write!(f, "Json convert error"),
        }
    }
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
        match Fail::find_root_cause(err).downcast_ref::<ErrorKind>() {
            Some(ErrorKind::NotFound(_)) => ModuleRuntimeErrorReason::NotFound,
            _ => ModuleRuntimeErrorReason::Other,
        }
    }
}
