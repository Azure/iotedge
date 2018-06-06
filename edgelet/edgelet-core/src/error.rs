// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;
use std::num::ParseIntError;

use edgelet_utils::Error as UtilsError;
use failure::{Backtrace, Context, Fail};
use tokio_timer;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "An IO error occurred.")]
    Io,
    #[fail(display = "A module runtime error occurred.")]
    ModuleRuntime,
    #[fail(display = "Signing error occurred. Invalid key length: {}", _0)]
    Sign(usize),
    #[fail(display = "A error occurred retrieving a key from the key store.")]
    KeyStore,
    #[fail(display = "Item not found.")]
    NotFound,
    #[fail(display = "Utils error")]
    Utils,
    #[fail(display = "Provisioning error")]
    Provision(String),
    #[fail(display = "Identity error")]
    Identity,
    #[fail(display = "Error activating key")]
    Activate,
    #[fail(display = "Edge runtime module has not been created in IoT Hub")]
    EdgeRuntimeNotFound,
    #[fail(display = "Watchdog error")]
    Watchdog,
    #[fail(display = "Tokio timer error")]
    TokioTimer,
    #[fail(display = "Module runtime returned module information without pid.")]
    ModuleRuntimeNoPid,
    #[fail(display = "Parse error.")]
    Parse,
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
    fn from(error: UtilsError) -> Error {
        Error {
            inner: error.context(ErrorKind::Utils),
        }
    }
}

impl From<tokio_timer::Error> for Error {
    fn from(error: tokio_timer::Error) -> Error {
        Error {
            inner: error.context(ErrorKind::TokioTimer),
        }
    }
}

impl From<ParseIntError> for Error {
    fn from(error: ParseIntError) -> Error {
        Error {
            inner: error.context(ErrorKind::Parse),
        }
    }
}
