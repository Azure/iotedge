// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use base64::DecodeError;
use failure::{Backtrace, Context, Fail};
use serde_json::Error as SerdeError;
use tokio::timer::Error as TimerError;

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
    Unexpected,
    #[fail(display = "Invalid Tpm token")]
    InvalidTpmToken,
    #[fail(display = "Assignment failed")]
    AssignmentFailed,
    #[fail(display = "Timer error")]
    TimerError,
    #[fail(display = "DPS operation not assigned")]
    NotAssigned,
    #[fail(display = "Error during keystore operation")]
    Keystore,
    #[fail(display = "Decode error")]
    Decode,
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

impl From<CoreError> for Error {
    fn from(error: CoreError) -> Self {
        Error {
            inner: error.context(ErrorKind::Core),
        }
    }
}

impl From<HttpError> for Error {
    fn from(error: HttpError) -> Self {
        Error {
            inner: error.context(ErrorKind::Http),
        }
    }
}

impl From<SerdeError> for Error {
    fn from(error: SerdeError) -> Self {
        Error {
            inner: error.context(ErrorKind::Serde),
        }
    }
}

impl From<Error> for HttpError {
    fn from(err: Error) -> Self {
        HttpError::from(err.context(HttpErrorKind::TokenSource))
    }
}

impl From<TimerError> for Error {
    fn from(error: TimerError) -> Self {
        Error {
            inner: error.context(ErrorKind::TimerError),
        }
    }
}

impl From<DecodeError> for Error {
    fn from(error: DecodeError) -> Self {
        Error {
            inner: error.context(ErrorKind::Decode),
        }
    }
}
