use std::fmt;

use failure::{Backtrace, Context, Fail};
use warp::reject::Reject;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>
}

#[derive(Copy, Clone, Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "Azure: {}", _0)]
    Azure(&'static str),
    #[fail(display = "Backend: {}", _0)]
    Backend(&'static str),
    #[fail(display = "CorruptData")]
    CorruptData,
    #[fail(display = "Forbidden")]
    Forbidden,
    #[fail(display = "KeyService: {}", _0)]
    KeyService(&'static str),
    #[fail(display = "NotFound")]
    NotFound,
    #[fail(display = "RandomNumberGenerator")]
    RandomNumberGenerator,
    #[fail(display = "Unauthorized")]
    Unauthorized
}

impl Reject for Error { }

impl Fail for Error {
    fn cause(&self) -> Option<&dyn Fail> {
        self.inner.cause()
    }

    fn backtrace(&self) -> Option<&Backtrace> {
        self.inner.backtrace()
    }
}

impl Error {
    pub fn kind(&self) -> ErrorKind {
        *self.inner.get_context()
    }
}

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Self {
        Self {
            inner: Context::new(kind)
        }
    }
}

impl From<Context<ErrorKind>> for Error {
    fn from(inner: Context<ErrorKind>) -> Self {
        Self {
            inner: inner
        }
    }
}

impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        fmt::Display::fmt(&self.inner, f)
    }
}
