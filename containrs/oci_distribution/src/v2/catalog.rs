use serde::{Deserialize, Serialize};

/// A sorted, json list of repositories available in the registry.
#[derive(Debug, Serialize, Deserialize)]
pub struct Catalog {
    #[serde(rename = "repositories")]
    repositories: Vec<String>,
}
