// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};

use edgelet_core::Error as CoreError;
use edgelet_utils::Error as UtilsError;
use iothubservice::error::{Error as HubServiceError, ErrorKind as HubServiceErrorKind};

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "An IO error occurred.")]
    Io,
    #[fail(display = "Edgelet utils error")]
    Utils(UtilsError),
    #[fail(display = "IoT Hub service error")]
    HubService,
    #[fail(display = "KeyStore could not fetch keys for module {}", _0)]
    CannotGetKey(String),
    #[fail(display = "Core error occurred.")]
    Core,
    #[fail(display = "Failed to get sas token.")]
    TokenSource,
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
    pub fn new(inner: Context<ErrorKind>) -> Error {
        Error { inner }
    }

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

impl From<HubServiceError> for Error {
    fn from(error: HubServiceError) -> Error {
        Error {
            inner: error.context(ErrorKind::HubService),
        }
    }
}

impl From<Error> for HubServiceError {
    fn from(error: Error) -> HubServiceError {
        HubServiceError::from(error.context(HubServiceErrorKind::Token))
    }
}

impl From<CoreError> for Error {
    fn from(error: CoreError) -> Error {
        Error {
            inner: error.context(ErrorKind::Core),
        }
    }
}
