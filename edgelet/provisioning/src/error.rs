// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use std::io::Error as IoError;

use base64::DecodeError;
use failure::{Backtrace, Context, Fail};
use regex::Error as RegexError;
use serde_json::Error as SerdeError;

use dps::{Error as DpsError, ErrorKind as DpsErrorKind};
use edgelet_core::{Error as CoreError, ErrorKind as CoreErrorKind};
use edgelet_http::Error as HttpError;
use edgelet_utils::Error as UtilsError;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "DPS error")]
    Dps,
    #[fail(display = "Utils error")]
    Utils,
    #[fail(display = "Item not found.")]
    NotFound,
    #[fail(display = "Provisioning error")]
    Provision(String),
    #[fail(display = "Core error")]
    Core,
    #[fail(display = "HSM error")]
    Hsm,
    #[fail(display = "Regex error")]
    Regex,
    #[fail(display = "Base64 decode error")]
    Base64,
    #[fail(display = "Http error")]
    Http,
    #[fail(display = "I/O error")]
    Io,
    #[fail(display = "Serde error")]
    Serde,
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

impl From<UtilsError> for Error {
    fn from(error: UtilsError) -> Error {
        Error {
            inner: error.context(ErrorKind::Utils),
        }
    }
}

impl From<DpsError> for Error {
    fn from(error: DpsError) -> Error {
        Error {
            inner: error.context(ErrorKind::Dps),
        }
    }
}

impl From<CoreError> for Error {
    fn from(error: CoreError) -> Error {
        Error {
            inner: error.context(ErrorKind::Core),
        }
    }
}

impl From<Error> for DpsError {
    fn from(err: Error) -> DpsError {
        DpsError::from(err.context(DpsErrorKind::Keystore))
    }
}

impl From<Error> for CoreError {
    fn from(err: Error) -> CoreError {
        CoreError::from(err.context(CoreErrorKind::Activate))
    }
}

impl From<RegexError> for Error {
    fn from(err: RegexError) -> Error {
        Error {
            inner: err.context(ErrorKind::Regex),
        }
    }
}

impl From<DecodeError> for Error {
    fn from(error: DecodeError) -> Error {
        Error {
            inner: error.context(ErrorKind::Base64),
        }
    }
}

impl From<HttpError> for Error {
    fn from(error: HttpError) -> Error {
        Error {
            inner: error.context(ErrorKind::Http),
        }
    }
}

impl From<IoError> for Error {
    fn from(error: IoError) -> Error {
        Error {
            inner: error.context(ErrorKind::Io),
        }
    }
}

impl From<SerdeError> for Error {
    fn from(error: SerdeError) -> Error {
        Error {
            inner: error.context(ErrorKind::Serde),
        }
    }
}
