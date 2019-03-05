// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "invalid base path {:?}", _0)]
    BadBasePath(String),

    #[fail(display = "URL {:?} is invalid - {}", _0, _1)]
    InvalidUrl(String, InvalidUrlReason),

    #[fail(display = "Could not construct named pipe URL")]
    ConstructUrlForHyper,
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

#[derive(Debug)]
pub enum InvalidUrlReason {
    BadHost(String),
    MissingHost,
    Path(String),
    Scheme(String),
}

impl fmt::Display for InvalidUrlReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            InvalidUrlReason::BadHost(s) => {
                write!(f, "could not decode host {:?} into valid pipe path", s)
            }
            InvalidUrlReason::MissingHost => write!(f, "no host"),
            InvalidUrlReason::Path(s) => write!(f, "path {:?} is not well-formed", s),
            InvalidUrlReason::Scheme(s) => write!(
                f,
                "scheme is {:?} but must be {:?}",
                s,
                super::NAMED_PIPE_SCHEME
            ),
        }
    }
}
