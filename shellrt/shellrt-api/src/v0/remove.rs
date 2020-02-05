use serde::{Deserialize, Serialize};

/// Remove a module
#[derive(Debug, Serialize, Deserialize)]
pub struct RemoveRequest {
    /// Module name
    pub name: String,
}

/// Returned once a RemoveRequest completes successfully
#[derive(Debug, Serialize, Deserialize)]
pub struct RemoveResponse {}
