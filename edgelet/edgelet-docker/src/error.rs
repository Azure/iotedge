// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};
use hyper::StatusCode;
use serde_json;

use docker::apis::{ApiError as DockerApiError, Error as DockerError};
use edgelet_core::{
    ModuleOperation, ModuleRuntimeErrorReason, RegistryOperation, RuntimeOperation,
};

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

fn get_message(
    error: DockerApiError<serde_json::Value>,
) -> ::std::result::Result<String, DockerApiError<serde_json::Value>> {
    let DockerApiError { code, content } = error;

    match content {
        Some(serde_json::Value::Object(props)) => {
            if let Some(serde_json::Value::String(message)) = props.get("message") {
                return Ok(message.clone());
            }

            Err(DockerApiError {
                code,
                content: Some(serde_json::Value::Object(props)),
            })
        }
        _ => Err(DockerApiError { code, content }),
    }
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
    DockerRuntime(DockerError<serde_json::Value>),

    #[fail(display = "{}", _0)]
    FormattedDockerRuntime(String),

    #[fail(display = "Could not initialize module runtime")]
    Initialization,

    #[fail(display = "Invalid docker image {:?}", _0)]
    InvalidImage(String),

    #[fail(display = "Invalid module name {:?}", _0)]
    InvalidModuleName(String),

    #[fail(display = "Invalid module type {:?}", _0)]
    InvalidModuleType(String),

    #[fail(display = "{}", _0)]
    ModuleOperation(ModuleOperation),

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

    pub fn from_docker_error(err: DockerError<serde_json::Value>, context: ErrorKind) -> Self {
        let context = match err {
            DockerError::Hyper(error) => error.context(ErrorKind::Docker).context(context),
            DockerError::Serde(error) => error.context(ErrorKind::Docker).context(context),
            DockerError::Api(error) => match error.code {
                StatusCode::NOT_FOUND => match get_message(error) {
                    Ok(message) => ErrorKind::NotFound(message).context(context),
                    Err(e) => ErrorKind::DockerRuntime(DockerError::Api(e)).context(context),
                },
                StatusCode::CONFLICT => ErrorKind::Conflict.context(context),
                StatusCode::NOT_MODIFIED => ErrorKind::NotModified.context(context),
                _ => match get_message(error) {
                    Ok(message) => ErrorKind::FormattedDockerRuntime(message).context(context),
                    Err(e) => ErrorKind::DockerRuntime(DockerError::Api(e)).context(context),
                },
            },
        };

        context.into()
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
