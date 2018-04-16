// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};
use hyper::{Error as HyperError, StatusCode};
use serde_json;
use url::ParseError;

use docker::apis::Error as DockerError;
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
    #[fail(display = "Edgelet utils error")]
    Utils(UtilsError),
    #[fail(display = "Serde error - {}", _0)]
    Serde(serde_json::Error),
    #[fail(display = "Transport error - {}", _0)]
    Transport(HyperError),
    #[fail(display = "Invalid URL")]
    UrlParse,
    #[fail(display = "Container not found")]
    NotFound,
    #[fail(display = "Conflict with current operation")]
    Conflict,
    #[fail(display = "Container already in this state")]
    NotModified,
    #[fail(display = "Docker runtime error - {:?}", _0)]
    Docker(DockerError<serde_json::Value>),
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
    fn from(err: UtilsError) -> Error {
        Error::from(ErrorKind::Utils(err))
    }
}

impl From<serde_json::Error> for Error {
    fn from(err: serde_json::Error) -> Error {
        Error::from(ErrorKind::Serde(err))
    }
}

impl From<HyperError> for Error {
    fn from(err: HyperError) -> Error {
        Error::from(ErrorKind::Transport(err))
    }
}

impl From<DockerError<serde_json::Value>> for Error {
    fn from(err: DockerError<serde_json::Value>) -> Error {
        match err {
            DockerError::Hyper(error) => Error::from(ErrorKind::Transport(error)),
            DockerError::Serde(error) => Error::from(ErrorKind::Serde(error)),
            DockerError::ApiError(ref error) if error.code == StatusCode::NotFound => {
                Error::from(ErrorKind::NotFound)
            }
            DockerError::ApiError(ref error) if error.code == StatusCode::Conflict => {
                Error::from(ErrorKind::Conflict)
            }
            DockerError::ApiError(ref error) if error.code == StatusCode::NotModified => {
                Error::from(ErrorKind::NotModified)
            }
            _ => Error::from(ErrorKind::Docker(err)),
        }
    }
}

impl From<ParseError> for Error {
    fn from(_: ParseError) -> Error {
        Error::from(ErrorKind::UrlParse)
    }
}
