// Copyright (c) Microsoft. All rights reserved.

//use std::fmt::{self, Display};
use std::io;

use thiserror::Error as ThisError;
use hyper::Error as HyperError;
use serde_json;
use chrono::ParseError;
use edgelet_client::Error as ClientError;
use edgelet_client::WorkloadError;

//use edgelet_http::Error as EdgeletHttpError;
//use workload::apis::Error as WorkloadError;


#[derive(Debug, ThisError)]
pub enum Error {
    #[error("Error connecting to endpoint")]
    Http,
    #[error("Hyper error")]
    Hyper(
        #[from]
        #[source]
        HyperError,
      ),
    #[error("An IO error occurred.")]
    Io(
        #[from]
        #[source]
        io::Error,
      ),
    #[error("Chrono date parse error")]
    Parse(
        #[from]
        #[source]
        ParseError,
      ),
    #[error("Serde error")]
    Serde(
        #[from]
        #[source]
        serde_json::Error,
      ),
    #[error("client error")]
    Client(
        #[from]
        #[source]
        ClientError,
      ),
    #[error("Workload api error")]
    Workload(
        #[from]
        #[source]
        WorkloadError,
      ),
}


