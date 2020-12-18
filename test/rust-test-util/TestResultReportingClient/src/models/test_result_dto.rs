use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};

use crate::MessageTestResult;

#[derive(Debug, Serialize, Deserialize, PartialEq)]
pub struct TestOperationResultDto {
    #[serde(rename = "source")]
    source: String,
    #[serde(rename = "result")]
    result: MessageTestResult,
    #[serde(rename = "type")]
    _type: u8,
    #[serde(rename = "createdAt")]
    created_at: String,
}

impl TestOperationResultDto {
    pub fn new(
        source: String,
        result: MessageTestResult,
        _type: TestType,
        created_at: DateTime<Utc>,
    ) -> Self {
        Self {
            source,
            result,
            _type: _type as u8,
            created_at: created_at.to_string(),
        }
    }
}

#[derive(Debug, Clone)]
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

#[cfg(test)]
mod tests {
    use chrono::Utc;

    use crate::{MessageTestResult, TestOperationResultDto};

    use super::TestType;

    #[test]
    fn serialize() {
        let tracking_id = "tracking".to_string();
        let batch_id = "batch".to_string();
        let seq_num = 2;
        let test_result = MessageTestResult::new(tracking_id, batch_id, seq_num);

        let source = "source".to_string();
        let _type = TestType::Messages;
        let created_at = Utc::now();
        let test_result_dto =
            TestOperationResultDto::new(source, test_result, _type.clone(), created_at.clone());

        let expected = format!("{{\"source\":\"source\",\"result\":\"tracking;batch;2\",\"type\":2,\"createdAt\":\"{}\"}}", created_at);
        let serialized = serde_json::to_string(&test_result_dto).unwrap();

        assert_eq!(expected, serialized);
    }

    #[test]
    fn deserialize() {
        let tracking_id = "tracking".to_string();
        let batch_id = "batch".to_string();
        let seq_num = 2;
        let test_result = MessageTestResult::new(tracking_id, batch_id, seq_num);

        let source = "source".to_string();
        let _type = TestType::Messages;
        let created_at = Utc::now();
        let expected =
            TestOperationResultDto::new(source, test_result, _type.clone(), created_at.clone());

        let serialized = format!(
            "{{\"source\":\"source\",\"result\":\"tracking;batch;2\",\"type\":2,\"createdAt\":\"{}\"}}",
            created_at
        );
        let deserialized: TestOperationResultDto =
            serde_json::from_str(serialized.as_str()).unwrap();

        assert_eq!(expected, deserialized);
    }
}
