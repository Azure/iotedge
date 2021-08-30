// Copyright (c) Microsoft. All rights reserved.

use std::io;
use thiserror::Error as ThisError;
use hyper::Error as HyperError;
use serde_json;
use chrono::ParseError;
use edgelet_client::Error as ClientError;
use edgelet_client::WorkloadError;



#[derive(Debug, ThisError)]
pub enum Error {

    #[error("no value for required '{0}'")]
    MissingVal(&'static str),
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
    #[error("Edgelet client error")]
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

