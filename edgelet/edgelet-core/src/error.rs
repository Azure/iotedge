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
    // Only used by edgelet-test-utils
    #[cfg(test)]
    #[fail(display = "Identity error")]
    Certificate,

    #[fail(
        display = "Edge runtime module has not been created in IoT Hub. Please make sure this device is an IoT Edge capable device."
    )]
    EdgeRuntimeIdentityNotFound,

    #[fail(display = "The timer that checks the edge runtime status encountered an error.")]
    EdgeRuntimeStatusCheckerTimer,

    #[fail(display = "An identity manager error occurred.")]
    IdentityManager,

    #[fail(display = "A error occurred in the key store.")]
    KeyStore,

    #[fail(display = "Invalid log tail {:?}", _0)]
    InvalidLogTail(String),

    #[fail(display = "Invalid module name {:?}", _0)]
    InvalidModuleName(String),

    #[fail(display = "Invalid module type {:?}", _0)]
    InvalidModuleType(String),

    #[fail(display = "Invalid URL {:?}", _0)]
    InvalidUrl(String),

    #[fail(display = "Item not found.")]
    KeyStoreItemNotFound,

    #[fail(display = "A module runtime error occurred.")]
    ModuleRuntime,

    #[fail(display = "Signing error occurred.")]
    Sign,

    #[fail(display = "Signing error occurred. Invalid key length: {}", _0)]
    SignInvalidKeyLength(usize),
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
