// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;
use std::io::Error as IoError;

use failure::{Backtrace, Context, Fail};
use git2;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Found submodule inconsistencies.")]
    Count(i64),
    #[fail(display = "Format Write error.")]
    Write,
    #[fail(display = "stdio error")]
    Stdio,
    #[fail(display = "Git library error")]
    Git,
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

impl From<i64> for Error {
    fn from(count: i64) -> Error {
        Error {
            inner: Context::new(ErrorKind::Count(count)),
        }
    }
}

impl From<fmt::Error> for Error {
    fn from(error: fmt::Error) -> Error {
        Error {
            inner: error.context(ErrorKind::Write),
        }
    }
}

impl From<IoError> for Error {
    fn from(error: IoError) -> Error {
        Error {
            inner: error.context(ErrorKind::Stdio),
        }
    }
}

impl From<git2::Error> for Error {
    fn from(error: git2::Error) -> Error {
        Error {
            inner: error.context(ErrorKind::Git),
        }
    }
}
