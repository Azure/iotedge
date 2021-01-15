#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use enumset::EnumSetType;
use hyper::http;

mod client;
mod models;

pub use client::TrcClient;
pub use models::message_result::MessageTestResult;

#[derive(Debug, EnumSetType)]
pub enum TestType {
    LegacyDirectMethod,
    LegacyTwin,
    Messages,
    DirectMethod,
    Twin,
    Network,
    Deployment,
    EdgeHubRestartMessage,
    EdgeHubRestartDirectMethod,
    Error,
    TestInfo,
}

#[derive(Debug, thiserror::Error)]
pub enum ReportResultError {
    #[error("failed converting test result data object to json string: {0:?}")]
    CreateJsonString(#[from] serde_json::error::Error),

    #[error("failed constructing request: {0:?}")]
    ConstructRequest(#[from] http::Error),

    #[error("failed sending request: {0:?}")]
    SendRequest(#[from] hyper::Error),

    #[error("response has failure status: {0:?}")]
    ResponseStatus(u16),

    #[error("unsupported test type specified")]
    UnsupportedTestType,
}
