// Copyright (c) Microsoft. All rights reserved.

use std::io;

use hyper::Error as HyperError;
use serde_json;
use url::ParseError;

use edgelet_http::Error as EdgeletHttpError;
use workload::apis::Error as WorkloadError;

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("Client error")]
    Client(WorkloadError<serde_json::Value>),
    #[error("Error connecting to endpoint")]
    Http(#[from] EdgeletHttpError),
    #[error("Hyper error")]
    Hyper(#[from] HyperError),
    #[error("An IO error occurred.")]
    Io(#[from] io::Error),
    #[error("Url parse error")]
    Parse(#[from] ParseError),
    #[error("Serde error")]
    Serde(#[from] serde_json::Error),
}

impl From<WorkloadError<serde_json::Value>> for Error {
    fn from(error: WorkloadError<serde_json::Value>) -> Self {
        match error {
            WorkloadError::Hyper(h) => From::from(h),
            WorkloadError::Serde(s) => From::from(s),
            WorkloadError::Api(_) => Error::Client(error),
        }
    }
}
