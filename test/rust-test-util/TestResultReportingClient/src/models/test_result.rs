use std::fmt::{self, Formatter};

use serde::{
    de::{self, Unexpected, Visitor},
    Deserialize, Deserializer, Serialize, Serializer,
};

#[derive(Debug, Serialize, Deserialize)]
pub struct TestOperationResultDto {
    #[serde(rename = "source")]
    source: String,
    #[serde(rename = "result")]
    result: TestResult,
    #[serde(rename = "type")]
    _type: String,
    #[serde(rename = "createdAt")]
    created_at: String,
}

impl TestOperationResultDto {
    pub fn new(source: String, result: TestResult, _type: String, created_at: String) -> Self {
        Self {
            source,
            result,
            _type,
            created_at,
        }
    }
}

#[derive(Debug)]
pub struct TestResult {
    tracking_id: String,
    batch_id: String,
    sequence_number: u32,
}

impl TestResult {
    pub fn new(tracking_id: String, batch_id: String, sequence_number: u32) -> Self {
        Self {
            tracking_id,
            batch_id,
            sequence_number,
        }
    }
}

struct CustomVisitor;

impl<'de> Visitor<'de> for CustomVisitor {
    type Value = TestResult;

    fn expecting(&self, formatter: &mut Formatter<'_>) -> fmt::Result {
        formatter.write_str("semicolon separated array")
    }

    fn visit_str<E>(self, value: &str) -> Result<TestResult, E>
    where
        E: de::Error,
    {
        let parts: Vec<String> = value.split(";").map(|part| part.to_string()).collect();
        let tracking_id = parts
            .get(0)
            .ok_or(de::Error::missing_field("tracking_id"))?;
        let batch_id = parts.get(1).ok_or(de::Error::missing_field("batch_id"))?;
        let sequence_number = parts
            .get(2)
            .ok_or(de::Error::missing_field("sequence_number"))?;

        let test_result = TestResult::new(
            tracking_id.clone(),
            batch_id.clone(),
            sequence_number.parse::<u32>().map_err(|_| {
                de::Error::invalid_type(
                    Unexpected::Str("sequence number should not be a string"),
                    &"sequence number as u32",
                )
            })?,
        );
        Ok(test_result)
    }
}

impl Serialize for TestResult {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        format!(
            "{};{};{}",
            self.tracking_id, self.batch_id, self.sequence_number
        )
        .serialize(serializer)
    }
}

impl<'de> Deserialize<'de> for TestResult {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        {
            deserializer.deserialize_identifier(CustomVisitor)
        }
    }
}

#[cfg(test)]
mod tests {
    use crate::{TestOperationResultDto, TestResult};

    #[test]
    fn serialize() {
        let tracking_id = "tracking".to_string();
        let batch_id = "batch".to_string();
        let seq_num = 2;
        let test_result = TestResult::new(tracking_id, batch_id, seq_num);

        let source = "source".to_string();
        let _type = "2".to_string();
        let created_at = "11/16/1996".to_string();
        let test_result_dto = TestOperationResultDto::new(source, test_result, _type, created_at);

        let expected = "{\"source\":\"source\",\"result\":\"tracking;batch;2\",\"type\":\"2\",\"createdAt\":\"11/16/1996\"}";
        let serialized = serde_json::to_string(&test_result_dto).unwrap();

        assert_eq!(expected, serialized);
    }
}
