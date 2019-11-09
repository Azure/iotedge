use std::collections::HashMap;

use serde::{Deserialize, Serialize};

/// Pull an image from a container registry.
#[derive(Debug, Serialize, Deserialize)]
pub struct ImgPullRequest {
    /// An image identifier (typically a docker-style image reference)
    pub image: String,
    /// Authentication credentials. Format will vary between plugins
    pub credentials: HashMap<String, String>,
}

/// Returned once a ImgPullRequest completes successfully
#[derive(Debug, Serialize, Deserialize)]
pub struct ImgPullResponse {}
