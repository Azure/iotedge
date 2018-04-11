// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;
use std::str;

use failure::{Backtrace, Context, Fail};
use hyper::{Error as HyperError, StatusCode};
use serde_json;
use url::ParseError;

use edgelet_utils::Error as UtilsError;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Edgelet utils error")]
    Utils(UtilsError),
    #[fail(display = "Serde error")]
    Serde(serde_json::Error),
    #[fail(display = "Url parse error")]
    Url(ParseError),
    #[fail(display = "Hyper HTTP error")]
    Hyper(HyperError),
    #[fail(display = "IoT Hub service error: [{}] {}", _0, _1)]
    HubServiceError(StatusCode, String),
}

impl<'a> From<(StatusCode, &'a [u8])> for Error {
    fn from(err: (StatusCode, &'a [u8])) -> Self {
        let (status_code, msg) = err;
        Error::from(ErrorKind::HubServiceError(
            status_code,
            str::from_utf8(msg)
                .unwrap_or_else(|_| "Could not decode error message")
                .to_string(),
        ))
    }
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

impl From<ParseError> for Error {
    fn from(err: ParseError) -> Error {
        Error::from(ErrorKind::Url(err))
    }
}

impl From<HyperError> for Error {
    fn from(err: HyperError) -> Error {
        Error::from(ErrorKind::Hyper(err))
    }
}
