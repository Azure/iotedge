// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};

use edgelet_core::ModuleRuntimeErrorReason;

// pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "{}", _0)]
    NotFound(String),

    #[fail(display = "TODO: Add error message")]
    Todo,
}

impl Fail for Error {
    fn cause(&self) -> Option<&dyn Fail> {
        self.inner.cause()
    }

    fn backtrace(&self) -> Option<&Backtrace> {
        self.inner.backtrace()
    }
}

impl Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
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

impl<'a> From<&'a Error> for ModuleRuntimeErrorReason {
    fn from(err: &'a Error) -> Self {
        match Fail::find_root_cause(err).downcast_ref::<ErrorKind>() {
            Some(ErrorKind::NotFound(_)) => ModuleRuntimeErrorReason::NotFound,
            _ => ModuleRuntimeErrorReason::Other,
        }
    }
}
