use serde::{Deserialize, Serialize};

/// Restart a module
#[derive(Debug, Serialize, Deserialize)]
pub struct RestartRequest {
    /// Module name
    pub name: String,
}

/// Returned once a RestartRequest completes successfully
#[derive(Debug, Serialize, Deserialize)]
pub struct RestartResponse {}
