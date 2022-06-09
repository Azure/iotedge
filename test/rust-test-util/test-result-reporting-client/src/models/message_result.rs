use serde::{Serialize, Serializer};

#[derive(Debug, PartialEq, Clone)]
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
