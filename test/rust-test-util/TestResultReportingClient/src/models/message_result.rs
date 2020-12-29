use std::fmt::{self, Formatter};

use serde::{
    de::{self, Unexpected, Visitor},
    Deserialize, Deserializer, Serialize, Serializer,
};

#[derive(Debug, PartialEq)]
pub struct MessageTestResult {
    tracking_id: String,
    batch_id: String,
    sequence_number: u32,
}

impl MessageTestResult {
    pub fn new(tracking_id: String, batch_id: String, sequence_number: u32) -> Self {
        Self {
            tracking_id,
            batch_id,
            sequence_number,
        }
    }
}

impl Serialize for MessageTestResult {
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

impl<'de> Deserialize<'de> for MessageTestResult {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        struct CustomVisitor;

        impl<'de> Visitor<'de> for CustomVisitor {
            type Value = MessageTestResult;

            fn expecting(&self, formatter: &mut Formatter<'_>) -> fmt::Result {
                formatter.write_str("semicolon separated array")
            }

            fn visit_str<E>(self, value: &str) -> Result<MessageTestResult, E>
            where
                E: de::Error,
            {
                let parts: Vec<String> = value.split(";").map(|part| part.to_string()).collect();
                let tracking_id = parts
                    .get(0)
                    .ok_or(de::Error::missing_field("tracking_id"))?
                    .to_string();
                let batch_id = parts.get(1).ok_or(de::Error::missing_field("batch_id"))?;
                let sequence_number = parts
                    .get(2)
                    .ok_or(de::Error::missing_field("sequence_number"))?;

                let test_result = MessageTestResult::new(
                    tracking_id,
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
        {
            deserializer.deserialize_identifier(CustomVisitor)
        }
    }
}
