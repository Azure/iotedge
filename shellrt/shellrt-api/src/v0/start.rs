use serde::{Deserialize, Serialize};

/// Start a module
#[derive(Debug, Serialize, Deserialize)]
pub struct StartRequest {
    /// Module name
    pub name: String,
}

/// Returned once a StartRequest completes successfully
#[derive(Debug, Serialize, Deserialize)]
pub struct StartResponse {}
