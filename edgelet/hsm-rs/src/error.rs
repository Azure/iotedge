// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;
use std::os::raw::c_int;
use std::str::Utf8Error;

use failure::{Backtrace, Context, Fail};

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Copy, Clone, Eq, PartialEq, Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "HSM Init failure: {}", _0)]
    Init(isize),
    #[fail(display = "HSM API failure occurred: {}", _0)]
    Api(c_int),
    #[fail(display = "HSM API Not Implemented")]
    NoneFn,
    #[fail(display = "HSM API failed to create Certificate properties")]
    CertProps,
    #[fail(display = "HSM API returned an invalid null response")]
    NullResponse,
    #[fail(display = "Could not parse bytes as utf-8")]
    Utf8,
    #[fail(display = "Invalid private key type: {}", _0)]
    PrivateKeyType(u32),
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

    pub fn kind(&self) -> ErrorKind {
        *self.inner.get_context()
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

impl From<c_int> for Error {
    fn from(result: c_int) -> Error {
        Error {
            inner: Context::new(ErrorKind::Api(result)),
        }
    }
}

impl From<isize> for Error {
    fn from(result: isize) -> Error {
        Error {
            inner: Context::new(ErrorKind::Init(result)),
        }
    }
}

impl From<Utf8Error> for Error {
    fn from(error: Utf8Error) -> Error {
        Error {
            inner: error.context(ErrorKind::Utf8),
        }
    }
}
