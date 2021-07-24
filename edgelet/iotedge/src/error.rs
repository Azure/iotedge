// Copyright (c) Microsoft. All rights reserved.

use std::fmt::Display;
use std::{fmt, process::ExitStatus};

use failure::{Backtrace, Context, Fail};

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Clone, Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Invalid value for --host parameter")]
    BadHostParameter,

    #[fail(display = "Invalid value for --since parameter")]
    BadSinceParameter,

    #[fail(display = "Invalid value for --tail parameter")]
    BadTailParameter,

    #[fail(display = "")]
    Diagnostics,

    #[fail(
        display = "Error while fetching latest versions of edge components: {}",
        _0
    )]
    FetchLatestVersions(FetchLatestVersionsReason),

    #[fail(
        display = "Could not determine versions of installed edge components: {}",
        _0
    )]
    DetermineEdgeVersion(DetermineEdgeVersionReason),

    #[fail(
        display = "Unknown platform. OS: {}, Arch: {}, Bitness: {}",
        os, arch, bitness
    )]
    UnknownPlatform {
        os: String,
        arch: String,
        bitness: String,
    },

    #[fail(display = "Command failed: {}", _0)]
    Config(std::borrow::Cow<'static, str>),

    #[fail(display = "Could not initialize tokio runtime")]
    InitializeTokio,

    #[fail(display = "Missing --host parameter")]
    MissingHostParameter,

    #[fail(display = "A module runtime error occurred")]
    ModuleRuntime,

    #[fail(display = "Could not generate support bundle")]
    SupportBundle,

    #[fail(display = "Could not write to stdout")]
    WriteToStdout,

    #[fail(display = "Could not write to file")]
    WriteToFile,

    #[fail(display = "Unable to bundle iotedge check")]
    BundleCheck,

    #[fail(display = "Unable to call docker inspect")]
    Docker,

    #[fail(display = "Error communicating with 'aziotctl' binary")]
    Aziot,

    #[fail(display = "Error running system command")]
    System,
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

#[derive(Clone, Copy, Debug)]
pub enum FetchLatestVersionsReason {
    CreateClient,
    GetResponse,
    InvalidOrMissingLocationHeader,
    ResponseStatusCode(hyper::StatusCode),
}

impl Display for FetchLatestVersionsReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            FetchLatestVersionsReason::CreateClient => write!(f, "could not create HTTP client"),
            FetchLatestVersionsReason::GetResponse => write!(f, "could not send HTTP request"),
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

#[derive(Clone, Debug)]
pub enum DetermineEdgeVersionReason {
    DockerInspectExitCode(ExitStatus, String),
    DockerInspectFailed(String),
    JsonParseError(String),
    ImageKeyNotFound,
    ImageValueUnexpectedFormat,
}

// TODO Consider making DetermineEdgeReason stateless as suggested in failure module documentation
impl Display for DetermineEdgeVersionReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            DetermineEdgeVersionReason::DockerInspectExitCode(exit_code, stderr) => write!(
                f,
                "docker inspect failed with exit code {}, stderr = {}",
                exit_code, stderr
            ),
            DetermineEdgeVersionReason::DockerInspectFailed(module_name) => write!(
                f,
                "docker inspect {} command failed to execute",
                module_name
            ),
            DetermineEdgeVersionReason::JsonParseError(module_name) => write!(
                f,
                "Error parsing JSON output from docker inspect {}",
                module_name
            ),
            DetermineEdgeVersionReason::ImageKeyNotFound => {
                write!(f, "'Image' key not found in docker inspect output")
            }
            DetermineEdgeVersionReason::ImageValueUnexpectedFormat => {
                write!(f, "'Image' value does not have expected sha256 format")
            }
        }
    }
}
