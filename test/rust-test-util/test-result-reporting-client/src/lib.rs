#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
    clippy::match_same_arms,
    clippy::must_use_candidate,
    clippy::missing_errors_doc,
    clippy::missing_panics_doc,
    clippy::expl_impl_clone_on_copy, // needed for `EnumSetType` derive
)]

use std::fmt;

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

impl fmt::Display for TestType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        fmt::Debug::fmt(self, f)
    }
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
