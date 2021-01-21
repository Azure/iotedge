use std::{iter::FromIterator, vec};

use chrono::{DateTime, Utc};
use enumset::EnumSet;
use hyper::{body, client::HttpConnector, Body, Client, Request};
use tracing::info;

use crate::{
    models::{message_result::MessageTestResult, test_result_dto::TestOperationResultDto},
    ReportResultError, TestType,
};

const CONTENT_TYPE: &str = "Content-Type";
const APPLICATION_JSON: &str = "application/json";

#[allow(clippy::module_name_repetitions)]
#[derive(Debug, Clone)]
pub struct TrcClient {
    client: Client<HttpConnector>,
    uri: String,
    supported_test_types: EnumSet<TestType>,
}

impl TrcClient {
    pub fn new(uri: String) -> Self {
        let supported_test_types = EnumSet::from_iter(vec![TestType::Messages]);

        Self {
            client: Client::new(),
            uri,
            supported_test_types,
        }
    }

    pub async fn report_result(
        &self,
        source: String,
        result: MessageTestResult,
        test_type: TestType,
        created_at: DateTime<Utc>,
    ) -> Result<(), ReportResultError> {
        if !self.supported_test_types.contains(test_type) {
            return Err(ReportResultError::UnsupportedTestType);
        }

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

        let status = response.status();
        let body_bytes = body::to_bytes(response.into_body()).await.unwrap();
        let body = String::from_utf8(body_bytes.to_vec()).expect("response was not valid utf-8");

        match status.as_u16() {
            204 | 200 => Ok(()),
            fail_status => {
                info!("failed response body: {:?}", body);
                Err(ReportResultError::ResponseStatus(fail_status))
            }
        }
    }
}
