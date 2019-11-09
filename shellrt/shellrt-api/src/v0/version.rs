use serde::{Deserialize, Serialize};

/// Retrieve a human-readable string detailing the runtime's version info
#[derive(Debug, Serialize, Deserialize)]
pub struct VersionRequest {}

/// Returned once a VersionRequest completes successfully
#[derive(Debug, Serialize, Deserialize)]
pub struct VersionResponse {
    /// Human-readable string detailing the runtime's verion info
    pub info: String,
}
