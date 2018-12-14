// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Clone, Copy, Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "The Connection String is missing required parameter {}", _0)]
    ConnStringMissingRequiredParameter(&'static str),

    #[fail(
        display = "The Connection String has a malformed value for parameter {}.",
        _0
    )]
    ConnStringMalformedParameter(&'static str),

    #[fail(display = "Could not backup provisioning result")]
    CouldNotBackup,

    #[fail(display = "Could not restore previous provisioning result")]
    CouldNotRestore,

    #[fail(display = "Could not initialize DPS provisioning client")]
    DpsInitialization,

    #[fail(
        display = "The Connection String is empty or invalid. Please update the config.yaml and provide the IoTHub connection information."
    )]
    InvalidConnString,

    #[fail(display = "Could not provision device")]
    Provision,
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
