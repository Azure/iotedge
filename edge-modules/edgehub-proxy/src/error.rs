// Copyright (c) Microsoft. All rights reserved.

use std::fmt::{self, Display};
use std::io;

use failure::{Backtrace, Context, Fail};
use hyper::Error as HyperError;
use serde_json;
use url::ParseError;

use edgelet_http::Error as EdgeletHttpError;
use workload::apis::Error as WorkloadError;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Client error")]
    Client(WorkloadError<serde_json::Value>),
    #[fail(display = "Error connecting to endpoint")]
    Http,
    #[fail(display = "Hyper error")]
    Hyper,
    #[fail(display = "An IO error occurred.")]
    Io,
    #[fail(display = "Url parse error")]
    Parse,
    #[fail(display = "Serde error")]
    Serde,
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

impl From<io::Error> for Error {
    fn from(error: io::Error) -> Self {
        Error {
            inner: error.context(ErrorKind::Io),
        }
    }
}

impl From<EdgeletHttpError> for Error {
    fn from(error: EdgeletHttpError) -> Self {
        Error {
            inner: error.context(ErrorKind::Http),
        }
    }
}

impl From<HyperError> for Error {
    fn from(error: HyperError) -> Self {
        Error {
            inner: error.context(ErrorKind::Hyper),
        }
    }
}

impl From<ParseError> for Error {
    fn from(error: ParseError) -> Self {
        Error {
            inner: error.context(ErrorKind::Parse),
        }
    }
}

impl From<serde_json::Error> for Error {
    fn from(error: serde_json::Error) -> Self {
        Error {
            inner: error.context(ErrorKind::Serde),
        }
    }
}

impl From<WorkloadError<serde_json::Value>> for Error {
    fn from(error: WorkloadError<serde_json::Value>) -> Self {
        match error {
            WorkloadError::Hyper(h) => From::from(h),
            WorkloadError::Serde(s) => From::from(s),
            WorkloadError::Api(_) => From::from(ErrorKind::Client(error)),
        }
    }
}
