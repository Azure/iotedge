use serde::{Deserialize, Serialize};

/// Retrieve a list of modules currently registered with the runtime
#[derive(Debug, Serialize, Deserialize)]
pub struct ListRequest {}

/// Returned once a ListRequest completes successfully
#[derive(Debug, Serialize, Deserialize)]
pub struct ListResponse {
    /// List of modules currently registered with the runtime
    pub modules: Vec<String>,
}
