use chrono::{DateTime, Utc};
use hyper::{client::HttpConnector, Body, Client, Request};

use crate::{
    models::{
        message_result::MessageTestResult,
        test_result_dto::{TestOperationResultDto, TestType},
    },
    ReportResultError,
};

const CONTENT_TYPE: &str = "Content-Type";
const APPLICATION_JSON: &str = "application/json";

#[derive(Debug)]
pub struct TrcClient {
    client: Client<HttpConnector>,
    uri: String,
}

impl TrcClient {
    pub fn new(uri: String) -> Self {
        Self {
            client: Client::new(),
            uri,
        }
    }

    pub async fn report_result(
        &self,
        source: String,
        result: MessageTestResult,
        test_type: TestType,
        created_at: DateTime<Utc>,
    ) -> Result<(), ReportResultError> {
        let body = TestOperationResultDto::new(source, result, test_type, created_at);
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

        match response.status().as_u16() {
            204 => Ok(()),
            200 => Ok(()),
            fail_status => Err(ReportResultError::ResponseStatus(fail_status)),
        }
    }
}
