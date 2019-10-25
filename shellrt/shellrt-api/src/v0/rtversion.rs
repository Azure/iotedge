use serde::{Deserialize, Serialize};

/// Retrieve a human-readable string detailing the runtime's version info
#[derive(Debug, Serialize, Deserialize)]
#[serde(tag = "type", rename = "version")]
pub struct RuntimeVersionRequest {}

/// Returned once a PullRequest completes successfully
#[derive(Debug, Serialize, Deserialize)]
pub struct RuntimeVersionResponse {
    pub info: String,
}

impl super::ReqMarker for RuntimeVersionRequest {}
impl super::ResMarker for RuntimeVersionResponse {}
