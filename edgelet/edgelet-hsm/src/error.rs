// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};
use hsm::Error as HsmError;
use edgelet_core::{Error as CoreError, ErrorKind as CoreErrorKind};

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Clone, Eq, PartialEq, Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "HSM failure")]
    Hsm,
    #[fail(display = "TPM system not enabled")]
    TpmNotEnabled,
    #[fail(display = "X509 system not enabled")]
    X509NotEnabled,
    #[fail(display = "Empty strings are not allowed")]
    EmptyStrings,
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

impl From<HsmError> for Error {
    fn from(error: HsmError) -> Error {
        Error {
            inner: error.context(ErrorKind::Hsm),
        }
    }
}

impl From<Error> for CoreError {
    fn from(error: Error) -> CoreError {
        CoreError::from(error.context(CoreErrorKind::KeyStore))
    }
}
