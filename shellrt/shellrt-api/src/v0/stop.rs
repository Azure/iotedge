use serde::{Deserialize, Serialize};

/// Stop a module
#[derive(Debug, Serialize, Deserialize)]
pub struct StopRequest {
    /// Module name
    pub name: String,
    /// Timeout in seconds to wait for the container to stop before forcibly
    /// terminating it.
    pub timeout: i64,
}

/// Returned once a StopRequest completes successfully
#[derive(Debug, Serialize, Deserialize)]
pub struct StopResponse {}
