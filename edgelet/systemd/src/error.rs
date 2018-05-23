// Copyright (c) Microsoft. All rights reserved.

use std::env::VarError;
use std::fmt;
use std::fmt::Display;
use std::num::ParseIntError;

use failure::{Backtrace, Context, Fail};
#[cfg(target_os = "linux")]
use nix::Error as NixError;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Missing required environment variable.")]
    Var,
    #[fail(display = "Error parsing pid.")]
    Parse,
    #[fail(display = "Environment variables meant for a different process.")]
    WrongProcess,
    #[fail(display = "Environment variable is invalid.")]
    InvalidVar,
    #[fail(display = "File descriptor is invalid.")]
    InvalidFd,
    #[cfg(target_os = "linux")]
    #[fail(display = "Syscall for socket failed.")]
    Nix,
    #[fail(display = "File descriptor not found.")]
    NotFound,
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

impl From<VarError> for Error {
    fn from(error: VarError) -> Error {
        Error {
            inner: error.context(ErrorKind::Var),
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

#[cfg(target_os = "linux")]
impl From<NixError> for Error {
    fn from(error: NixError) -> Error {
        Error {
            inner: error.context(ErrorKind::Nix),
        }
    }
}
