// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;
use std::os::raw::c_int;

use failure::{Backtrace, Context, Fail};

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Copy, Clone, Eq, PartialEq, Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "HSM Init failure")] Init(isize),
    #[fail(display = "HSM API failure occured")] Api(c_int),
    #[fail(display = "HSM API Not Implemented")] NoneFn,
    #[fail(display = "HSM API returned an invalid null response")] NullResponse,
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
