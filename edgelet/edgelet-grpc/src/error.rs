// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;
use std::io;

use failure::{Backtrace, Context, Fail};
use tower_h2::client::{Error as H2ClientError, HandshakeError};

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Copy, Clone, Eq, PartialEq, Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "An IO error occurred.")] Io,
    #[fail(display = "An error occured in the core service.")] Core,
    #[fail(display = "Invalid Url.")] Url,
    #[fail(display = "An error occurred in the h2 layer.")] H2,
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

impl From<io::Error> for Error {
    fn from(error: io::Error) -> Error {
        Error {
            inner: error.context(ErrorKind::Io),
        }
    }
}

impl From<H2ClientError> for Error {
    fn from(_: H2ClientError) -> Error {
        ErrorKind::H2.into()
    }
}

impl From<HandshakeError> for Error {
    fn from(_: HandshakeError) -> Error {
        ErrorKind::H2.into()
    }
}
