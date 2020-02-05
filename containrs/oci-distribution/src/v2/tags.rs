use serde::{Deserialize, Serialize};

/// A list of tags for the named repository.
#[derive(Debug, Serialize, Deserialize)]
pub struct Tags {
    #[serde(rename = "name")]
    name: String,
    #[serde(rename = "tags")]
    tags: Vec<String>,
}
