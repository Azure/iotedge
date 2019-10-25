use serde::{Deserialize, Serialize};

/// Remove an image from disk.
#[derive(Debug, Serialize, Deserialize)]
#[serde(tag = "type", rename = "remove")]
pub struct RemoveRequest {
    /// An image identifier (typically a docker-style image reference)
    pub image: String,
}

/// Returned once a RemoveRequest completes successfully
#[derive(Debug, Serialize, Deserialize)]
pub struct RemoveResponse {}

impl super::ReqMarker for RemoveRequest {}
impl super::ResMarker for RemoveResponse {}
