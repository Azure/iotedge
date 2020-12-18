use hyper::http;

mod client;
mod models;

pub use client::TestResultReportingClient;
pub use models::test_result::TestOperationResultDto;
pub use models::test_result::TestResult;

#[derive(Debug, thiserror::Error)]
pub enum ReportResultError {
    #[error("failed converting test result data object to json string")]
    CreateJsonString(#[source] serde_json::error::Error),

    #[error("failed constructing request")]
    ConstructRequest(#[source] http::Error),

    #[error("failed sending request")]
    SendRequest(#[source] hyper::Error),
}
