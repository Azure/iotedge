// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;
use std::io;

use edgelet_http_mgmt::Error as HttpMgmtError;
use failure::{Backtrace, Context, Fail};
use url::ParseError;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "A module runtime error occurred.")]
    ModuleRuntime,
    #[fail(display = "An IO error occurred.")]
    Io,
    #[fail(display = "Cannot parse uri")]
    UrlParse,
    #[fail(display = "An error in the management http client occurred.")]
    HttpMgmt,
    #[fail(display = "Missing host")]
    NoHost,
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

impl From<io::Error> for Error {
    fn from(error: io::Error) -> Error {
        Error {
            inner: error.context(ErrorKind::Io),
        }
    }
}

impl From<ParseError> for Error {
    fn from(error: ParseError) -> Error {
        Error {
            inner: error.context(ErrorKind::UrlParse),
        }
    }
}

impl From<HttpMgmtError> for Error {
    fn from(error: HttpMgmtError) -> Error {
        Error {
            inner: error.context(ErrorKind::HttpMgmt),
        }
    }
}
