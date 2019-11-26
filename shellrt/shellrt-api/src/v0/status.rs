use serde::{Deserialize, Serialize};

/// Lifecycle condition of the module
#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
#[allow(missing_docs)] // names are self-explanatory
pub enum ModuleStatus {
    Unknown,
    Running,
    Stopped,
    Failed,
}

/// Query the status of a module
#[derive(Debug, Serialize, Deserialize)]
pub struct StatusRequest {
    /// Module name
    pub name: String,
}

/// Returned once a StatusRequest completes successfully
///
/// NOTE: unlike the ModuleRuntime type in edgelet, this response doesn't
/// include the top-level PID. The top level PID (along with all other PIDs in
/// the container) can be optained via "TopRequest"
#[derive(Debug, Serialize, Deserialize)]
pub struct StatusResponse {
    /// Status of the container
    pub status: ModuleStatus,
    /// Human-readable message indicating details about why container is in its
    /// current state
    pub status_description: String,
    /// Reference to the image in use.
    pub image_id: String,
    /// Start time of the container in nanoseconds.
    pub started_at: i64,
    /// Finish time of the container in nanoseconds
    /// (only set when the container has been terminated)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub finished_at: Option<i64>,
    /// Exit code of the container.
    /// (only set when the container has been terminated)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub exit_code: Option<i64>,
}
