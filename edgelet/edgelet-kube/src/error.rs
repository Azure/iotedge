// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use config::ConfigError;
use edgelet_core::{ModuleRuntimeErrorReason, RuntimeOperation};
use edgelet_docker::Error as DockerError;
use failure::{Backtrace, Context, Fail};
use hyper::Error as HyperError;
use kube_client::Error as KubeClientError;
use serde_json::Error as JsonError;
use typed_headers::Error as HeaderError;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "Kubernetes error")]
    Kubernetes,

    #[fail(display = "Could not initialize module runtime")]
    Initialization,

    #[fail(display = "Invalid module name {:?}", _0)]
    InvalidModuleName(String),

    #[fail(display = "Device Id was not found")]
    MissingDeviceId,

    #[fail(display = "IoT Hub name was not found")]
    MissingHubName,

    #[fail(display = "Container not found in module, name = {:?}", _0)]
    ModuleNotFound(String),

    #[fail(display = "Image not found in PodSpec")]
    ImageNotFound,

    #[fail(display = "Invalid Runtime parameter {:?} : {:?}", _0, _1)]
    InvalidRunTimeParameter(String, String),

    #[fail(display = "{}", _0)]
    RuntimeOperation(RuntimeOperation),

    #[fail(display = "Invalid authentication token")]
    ModuleAuthenticationError,

    #[fail(display = "Auth name not valid")]
    AuthName,

    #[fail(display = "Auth server address not present ")]
    AuthServerAddress,

    #[fail(display = "Auth user name not present")]
    AuthUser,

    #[fail(display = "Auth password not present")]
    AuthPassword,

    #[fail(display = "Metadata missing from Deployment")]
    DeploymentMeta,

    #[fail(display = "Name field missing from Deployment")]
    DeploymentName,

    #[fail(display = "Kubernetes client error")]
    KubeClient,

    #[fail(display = "Docker crate error")]
    DockerError,

    #[fail(display = "Json convert error")]
    JsonError,

    #[fail(display = "{}", _0)]
    NotFound(String),

    #[fail(display = "Config parsing error")]
    Config,

    #[fail(display = "HTTP connection error")]
    Hyper,
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

impl From<KubeClientError> for Error {
    fn from(error: KubeClientError) -> Self {
        Error {
            inner: error.context(ErrorKind::KubeClient),
        }
    }
}

impl From<DockerError> for Error {
    fn from(error: DockerError) -> Self {
        Error {
            inner: error.context(ErrorKind::DockerError),
        }
    }
}

impl From<JsonError> for Error {
    fn from(error: JsonError) -> Self {
        Error {
            inner: error.context(ErrorKind::JsonError),
        }
    }
}

impl From<HeaderError> for Error {
    fn from(error: HeaderError) -> Self {
        Error {
            inner: error.context(ErrorKind::ModuleAuthenticationError),
        }
    }
}

impl From<ConfigError> for Error {
    fn from(error: ConfigError) -> Self {
        Error {
            inner: error.context(ErrorKind::Config),
        }
    }
}

impl From<HyperError> for Error {
    fn from(error: HyperError) -> Self {
        Error {
            inner: error.context(ErrorKind::Hyper),
        }
    }
}
