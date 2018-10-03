// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};
use hyper::{Error as HyperError, StatusCode};
use serde_json;
use url::ParseError;

use docker::apis::Error as DockerError;
use edgelet_core::{Error as CoreError, ErrorKind as CoreErrorKind};
use edgelet_http::Error as HttpError;
use edgelet_utils::Error as UtilsError;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Invalid docker URI - {}", _0)]
    InvalidDockerUri(String),
    #[fail(display = "Invalid unix domain socket URI - {}", _0)]
    InvalidUdsUri(String),
    #[fail(display = "Utils error")]
    Utils,
    #[fail(display = "Serde error")]
    Serde,
    #[fail(display = "Transport error")]
    Transport,
    #[fail(display = "Invalid URL")]
    UrlParse,
    #[fail(display = "Not found")]
    NotFound,
    #[fail(display = "Conflict with current operation")]
    Conflict,
    #[fail(display = "Container already in this state")]
    NotModified,
    #[fail(display = "Container runtime error")]
    Docker,
    #[fail(display = "Container runtime error - {:?}", _0)]
    DockerRuntime(DockerError<serde_json::Value>),
    #[fail(display = "Core error")]
    Core,
    #[fail(display = "Http error")]
    Http,
}

impl Fail for Error {
    fn cause(&self) -> Option<&Fail> {
        self.inner.cause()
    }

    fn backtrace(&self) -> Option<&Backtrace> {
        self.inner.backtrace()
    }
}

impl Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        Display::fmt(&self.inner, f)
    }
}

impl Error {
    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
    }
}

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Error {
        Error {
            inner: Context::new(kind),
        }
    }
}

impl From<Context<ErrorKind>> for Error {
    fn from(inner: Context<ErrorKind>) -> Error {
        Error { inner }
    }
}

impl From<UtilsError> for Error {
    fn from(error: UtilsError) -> Error {
        Error {
            inner: error.context(ErrorKind::Utils),
        }
    }
}

impl From<serde_json::Error> for Error {
    fn from(error: serde_json::Error) -> Error {
        Error {
            inner: error.context(ErrorKind::Serde),
        }
    }
}

impl From<HyperError> for Error {
    fn from(error: HyperError) -> Error {
        Error {
            inner: error.context(ErrorKind::Transport),
        }
    }
}

impl From<HttpError> for Error {
    fn from(error: HttpError) -> Error {
        Error {
            inner: error.context(ErrorKind::Http),
        }
    }
}

impl From<DockerError<serde_json::Value>> for Error {
    fn from(err: DockerError<serde_json::Value>) -> Error {
        match err {
            DockerError::Hyper(error) => Error {
                inner: Error::from(error).context(ErrorKind::Docker),
            },
            DockerError::Serde(error) => Error {
                inner: Error::from(error).context(ErrorKind::Docker),
            },
            DockerError::ApiError(ref error) if error.code == StatusCode::NOT_FOUND => {
                Error::from(ErrorKind::NotFound)
            }
            DockerError::ApiError(ref error) if error.code == StatusCode::CONFLICT => {
                Error::from(ErrorKind::Conflict)
            }
            DockerError::ApiError(ref error) if error.code == StatusCode::NOT_MODIFIED => {
                Error::from(ErrorKind::NotModified)
            }
            _ => Error::from(ErrorKind::DockerRuntime(err)),
        }
    }
}

impl From<ParseError> for Error {
    fn from(_: ParseError) -> Error {
        Error::from(ErrorKind::UrlParse)
    }
}

impl From<Error> for CoreError {
    fn from(err: Error) -> CoreError {
        CoreError::from(err.context(CoreErrorKind::ModuleRuntime))
    }
}
