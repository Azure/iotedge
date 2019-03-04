// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};
use hsm::Error as HsmError;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Clone, Copy, Eq, PartialEq, Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "HSM failure")]
    Hsm,
    #[fail(display = "TPM system not enabled")]
    TpmNotEnabled,
    #[fail(display = "X509 system not enabled")]
    X509NotEnabled,
    #[fail(display = "Empty strings are not allowed")]
    EmptyStrings,
    #[fail(display = "Only Device keys are allowed to be activated")]
    NoModuleActivation,
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
    pub fn new(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }

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

impl From<HsmError> for Error {
    fn from(error: HsmError) -> Self {
        Error {
            inner: error.context(ErrorKind::Hsm),
        }
    }
}
