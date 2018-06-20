// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;
use std::io::Error as IoError;

use edgelet_utils::Error as UtilsError;
use failure::{Backtrace, Context, Fail};
use log::{ParseLevelError, SetLoggerError};

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Edgelet utils error")]
    Utils,
    #[fail(display = "I/O error")]
    Io,
    #[fail(display = "Log level parse error")]
    ParseLevel,
    #[fail(display = "Could not set global logger")]
    SetLogger,
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

impl From<IoError> for Error {
    fn from(err: IoError) -> Error {
        Error {
            inner: err.context(ErrorKind::Io),
        }
    }
}

impl From<UtilsError> for Error {
    fn from(err: UtilsError) -> Error {
        Error {
            inner: err.context(ErrorKind::Utils),
        }
    }
}

impl From<ParseLevelError> for Error {
    fn from(_: ParseLevelError) -> Error {
        Error::from(ErrorKind::ParseLevel)
    }
}

impl From<SetLoggerError> for Error {
    fn from(_: SetLoggerError) -> Error {
        Error::from(ErrorKind::SetLogger)
    }
}
