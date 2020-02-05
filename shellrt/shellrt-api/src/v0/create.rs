use std::collections::HashMap;

use serde::{Deserialize, Serialize};

/// Create a new module
#[derive(Debug, Serialize, Deserialize)]
pub struct CreateRequest {
    /// Module name
    pub name: String,
    /// Environment variable overrides
    pub env: HashMap<String, String>,
    /// Runtime-specific config type
    pub config_type: String,
    /// Runtime-specific config options
    pub config: serde_json::Value,
}

/// Returned once a CreateRequest completes successfully
#[derive(Debug, Serialize, Deserialize)]
pub struct CreateResponse {}
