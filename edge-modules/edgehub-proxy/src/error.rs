// Copyright (c) Microsoft. All rights reserved.

use std::fmt::{self, Display};
use std::io;

use thiserror::Error;
use hyper::Error as HyperError;
use serde_json;
use url::ParseError;

//use edgelet_http::Error as EdgeletHttpError;
use workload::apis::Error as WorkloadError;


#[derive(Error, Debug)]
pub enum Error {
    #[error("Client error")]
    Client(WorkloadError<serde_json::Value>),
    #[error("Error connecting to endpoint")]
    Http,
    #[error("Hyper error")]
    Hyper,
    #[error("An IO error occurred.")]
    Io,
    #[error("Url parse error")]
    Parse,
    #[error("Serde error")]
    Serde,
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
