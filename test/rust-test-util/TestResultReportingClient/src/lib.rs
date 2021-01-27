#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use hyper::http;
use enumset::EnumSetType;

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
    #[error("failed converting test result data object to json string")]
    CreateJsonString(#[source] serde_json::error::Error),

    #[error("failed constructing request")]
    ConstructRequest(#[source] http::Error),

    #[error("failed sending request")]
    SendRequest(#[source] hyper::Error),

    #[error("response has failure status {}", 0)]
    ResponseStatus(u16),

    #[error("unsupported test type specified")]
    UnsupportedTestType,
}
