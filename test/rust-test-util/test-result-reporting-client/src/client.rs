use std::{iter::FromIterator, time::Duration, vec};

use chrono::{DateTime, Utc};
use enumset::EnumSet;
use hyper::{body, client::HttpConnector, Body, Client, Request};
use rand::Rng;
use tokio::time;
use tracing::warn;

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

        // exponential randomized backoff for max ~3 mins (plus whatever internal time taken by http calls)
        let mut retries: u32 = 11;
        let mut base_wait = Duration::from_millis(100);
        loop {
            let body =
                TestOperationResultDto::new(source.clone(), result.clone(), test_type, created_at);
            let body = serde_json::to_string(&body).map_err(ReportResultError::CreateJsonString)?;
            let request = Request::post(self.uri.clone())
                .header(CONTENT_TYPE, APPLICATION_JSON)
                .body(Body::from(body.clone()))
                .map_err(ReportResultError::ConstructRequest)?;

            match self.trc_request(request).await {
                Err(e) if retries > 0 => {
                    warn!("request to trc failed: {:?}", e);

                    let rand_num = rand::thread_rng().gen_range(1..10);
                    let sleep_duration = base_wait + Duration::from_millis(rand_num * 100);

                    retries -= 1;
                    time::sleep(sleep_duration).await;
                    base_wait *= 2
                }
                response => return response,
            }
        }
    }

    async fn trc_request(&self, request: Request<Body>) -> Result<(), ReportResultError> {
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
                warn!("failed response body: {:?}", body);
                Err(ReportResultError::ResponseStatus(fail_status))
            }
        }
    }
}
