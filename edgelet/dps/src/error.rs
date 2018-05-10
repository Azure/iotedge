// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};
use serde_json::Error as SerdeError;

use edgelet_core::Error as CoreError;
use edgelet_http::{Error as HttpError, ErrorKind as HttpErrorKind};

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Core error")]
    Core,
    #[fail(display = "Http error")]
    Http,
    #[fail(display = "Serde error")]
    Serde,
    #[fail(display = "DPS returned an empty response when a value was expected")]
    EmptyResponse,
    #[fail(display = "Invalid Tpm token")]
    InvalidTpmToken,
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

impl From<CoreError> for Error {
    fn from(error: CoreError) -> Error {
        Error {
            inner: error.context(ErrorKind::Core),
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

impl From<SerdeError> for Error {
    fn from(error: SerdeError) -> Error {
        Error {
            inner: error.context(ErrorKind::Serde),
        }
    }
}

impl From<Error> for HttpError {
    fn from(err: Error) -> HttpError {
        HttpError::from(err.context(HttpErrorKind::TokenSource))
    }
}
