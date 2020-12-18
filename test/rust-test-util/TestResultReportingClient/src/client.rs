use hyper::{client::HttpConnector, Body, Client, Request};

use crate::{
    models::test_result::{TestOperationResultDto, TestResult},
    ReportResultError,
};

const CONTENT_TYPE: &str = "Content-Type";
const APPLICATION_JSON: &str = "application/json";

#[derive(Debug)]
pub struct TestResultReportingClient {
    client: Client<HttpConnector>,
    uri: String,
}

impl TestResultReportingClient {
    pub fn new(uri: String) -> Self {
        Self {
            client: Client::new(),
            uri,
        }
    }

    pub async fn report_result(
        &self,
        source: String,
        result: TestResult,
        _type: String,
        created_at: String,
    ) -> Result<(), ReportResultError> {
        let body = TestOperationResultDto::new(source, result, _type, created_at);
        let body = serde_json::to_string(&body).map_err(ReportResultError::CreateJsonString)?;
        let request = Request::post(self.uri.clone())
            .header(CONTENT_TYPE, APPLICATION_JSON)
            .body(Body::from(body.clone()))
            .map_err(ReportResultError::ConstructRequest)?;
        let response = self
            .client
            .request(request)
            .await
            .map_err(ReportResultError::SendRequest)?;

        // TODO: parse response

        Ok(())
    }
}
