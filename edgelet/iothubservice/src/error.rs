// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;
use std::str;

use failure::{Backtrace, Context, Fail};
use hyper::{Error as HyperError, StatusCode};
use serde_json;
use url::ParseError;

use edgelet_http::{Error as HttpError, ErrorKind as HttpErrorKind};
use edgelet_utils::Error as UtilsError;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Utils error")]
    Utils,
    #[fail(display = "Core error")]
    Http,
    #[fail(display = "Serde error")]
    Serde,
    #[fail(display = "Url parse error")]
    Url,
    #[fail(display = "Hyper HTTP error")]
    Hyper,
    #[fail(display = "IoT Hub service error: [{}] {}", _0, _1)]
    HubServiceError(StatusCode, String),
    #[fail(display = "IoT Hub returned an empty response when a value was expected")]
    EmptyResponse,
    #[fail(display = "Module not found")]
    ModuleNotFound,
    #[fail(display = "Module ID is empty")]
    EmptyModuleId,
    #[fail(display = "Failed to get sas token")]
    Token,
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
    fn from(error: UtilsError) -> Error {
        Error {
            inner: error.context(ErrorKind::Utils),
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

impl From<serde_json::Error> for Error {
    fn from(error: serde_json::Error) -> Error {
        Error {
            inner: error.context(ErrorKind::Serde),
        }
    }
}

impl From<ParseError> for Error {
    fn from(error: ParseError) -> Error {
        Error {
            inner: error.context(ErrorKind::Url),
        }
    }
}

impl From<HyperError> for Error {
    fn from(error: HyperError) -> Error {
        Error {
            inner: error.context(ErrorKind::Hyper),
        }
    }
}

impl From<Error> for HttpError {
    fn from(err: Error) -> HttpError {
        HttpError::from(err.context(HttpErrorKind::TokenSource))
    }
}
