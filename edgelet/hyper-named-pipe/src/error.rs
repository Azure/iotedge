// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use std::str::Utf8Error;

use failure::{Backtrace, Context, Fail};
use hex::FromHexError;
use url::ParseError;

use edgelet_utils::Error as UtilsError;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "URL scheme is missing or is not npipe")]
    InvalidUrlScheme,
    #[fail(display = "URL host name has not been specified")]
    MissingUrlHost,
    #[fail(display = "Named pipe URL is not well formed")]
    MalformedNamedPipeUrl,
    #[fail(display = "Invalid URL")]
    UrlParse,
    #[fail(display = "Edgelet utils error")]
    Utils,
    #[fail(display = "Hex encode/decode error")]
    Hex,
    #[fail(display = "UTF-8 encode/decode error")]
    Utf8,
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

impl From<ParseError> for Error {
    fn from(err: ParseError) -> Error {
        Error {
            inner: err.context(ErrorKind::UrlParse),
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

impl From<FromHexError> for Error {
    fn from(err: FromHexError) -> Error {
        Error {
            inner: err.context(ErrorKind::Hex),
        }
    }
}

impl From<Utf8Error> for Error {
    fn from(err: Utf8Error) -> Error {
        Error {
            inner: err.context(ErrorKind::Utf8),
        }
    }
}
