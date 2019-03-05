// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};

use edgelet_core::IdentityOperation;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "KeyStore could not fetch keys for module {}", _0)]
    CannotGetKey(String),

    #[fail(display = "Could not create identity {}: {}", _0, _1)]
    CreateIdentityWithReason(String, IdentityOperationReason),

    #[fail(display = "Could not get SAS token")]
    GetToken,

    #[fail(display = "{}", _0)]
    IdentityOperation(IdentityOperation),

    #[fail(display = "Could not update identity {}: {}", _0, _1)]
    UpdateIdentityWithReason(String, IdentityOperationReason),
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

#[derive(Clone, Copy, Debug)]
pub enum IdentityOperationReason {
    InvalidHubResponse,
    MissingGenerationId,
}

impl fmt::Display for IdentityOperationReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            IdentityOperationReason::InvalidHubResponse => write!(f, "Invalid IoT Hub response"),
            IdentityOperationReason::MissingGenerationId => {
                write!(f, "Generation Id was not provided")
            }
        }
    }
}
