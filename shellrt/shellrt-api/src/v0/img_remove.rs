use serde::{Deserialize, Serialize};

/// Remove an image from disk.
#[derive(Debug, Serialize, Deserialize)]
pub struct ImgRemoveRequest {
    /// An image identifier (typically a docker-style image reference)
    pub image: String,
}

/// Returned once a ImgRemoveRequest completes successfully
#[derive(Debug, Serialize, Deserialize)]
pub struct ImgRemoveResponse {}
