// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};
use hyper::StatusCode;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "Could not delete module")]
    DeleteModule,

    #[fail(display = "Could not delete module {}: {}", _0, _1)]
    DeleteModuleWithReason(String, Reason),

    #[fail(display = "Could not get module {}", _0)]
    GetModule(String),

    #[fail(display = "Could not get module {}: {}", _0, _1)]
    GetModuleWithReason(String, Reason),

    #[fail(display = "IoT Hub service error: [{}] {}", _0, _1)]
    HubService(StatusCode, String),

    #[fail(display = "Invalid device ID {:?}", _0)]
    InvalidDeviceId(String),

    #[fail(display = "Could not list modules")]
    ListModules,

    #[fail(display = "Could not list modules: {}", _0)]
    ListModulesWithReason(Reason),

    #[fail(display = "Could not upsert module {}", _0)]
    UpsertModule(String),

    #[fail(display = "Could not upsert module {}: {}", _0, _1)]
    UpsertModuleWithReason(String, Reason),
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

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum Reason {
    EmptyModuleId,
    EmptyResponse,
    ModuleNotFound,
}

impl Display for Reason {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match self {
            Reason::EmptyModuleId => write!(f, "Module ID is empty"),
            Reason::EmptyResponse => write!(f, "IoT Hub returned an empty response when a value was expected"),
            Reason::ModuleNotFound => write!(f, "Module not found"),
        }
    }
}
