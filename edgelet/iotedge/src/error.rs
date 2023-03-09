// Copyright (c) Microsoft. All rights reserved.

use std::fmt;

#[derive(Clone, Debug, thiserror::Error)]
pub enum Error {
    #[error("Invalid value for --host parameter")]
    BadHostParameter,

    #[error("Invalid value for --since parameter")]
    BadSinceParameter,

    #[error("Invalid value for --tail parameter")]
    BadTailParameter,

    #[error("")]
    Diagnostics,

    #[error("Error while fetching latest versions of edge components: {0}")]
    FetchLatestVersions(FetchLatestVersionsReason),

    #[error("Command failed: {0}")]
    Config(std::borrow::Cow<'static, str>),

    #[error("Could not initialize tokio runtime")]
    InitializeTokio,

    #[error("Missing --host parameter")]
    MissingHostParameter,

    #[error("A module runtime error occurred")]
    ModuleRuntime,

    #[error("Could not generate support bundle")]
    SupportBundle,

    #[error("Could not write to stdout")]
    WriteToStdout,

    #[error("Could not write to file")]
    WriteToFile,

    #[error("Unable to bundle iotedge check")]
    BundleCheck,

    #[error("Unable to call docker: {0}")]
    Docker(String),

    #[error("Error communicating with 'aziotctl' binary")]
    Aziot,

    #[error("Error running system command")]
    System,

    #[error("Error running check: {0}")]
    Check(String),

    #[error("{0}")]
    Misc(String),
}

#[derive(Clone, Copy, Debug)]
pub enum FetchLatestVersionsReason {
    RequestTimeout,
    CreateClient,
    GetResponse,
    InvalidOrMissingLocationHeader,
    ResponseStatusCode(hyper::StatusCode),
}

impl fmt::Display for FetchLatestVersionsReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            FetchLatestVersionsReason::CreateClient => write!(f, "could not create HTTP client"),
            FetchLatestVersionsReason::GetResponse => write!(f, "could not send HTTP request"),
            FetchLatestVersionsReason::RequestTimeout => write!(f, "HTTP request timed out"),
            FetchLatestVersionsReason::InvalidOrMissingLocationHeader => write!(
                f,
                "redirect response has invalid or missing location header"
            ),
            FetchLatestVersionsReason::ResponseStatusCode(status_code) => {
                write!(f, "response failed with status code {}", status_code)
            }
        }
    }
}
