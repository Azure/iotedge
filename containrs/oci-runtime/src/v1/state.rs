use std::collections::HashMap;

use serde::{Deserialize, Serialize};

/// State holds information about the runtime state of the container.
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct State {
    /// Version is the version of the specification that is supported.
    #[serde(rename = "ociVersion")]
    pub version: String,
    /// ID is the container ID
    #[serde(rename = "id")]
    pub id: String,
    /// Status is the runtime status of the container.
    #[serde(rename = "status")]
    pub status: String,
    /// Pid is the process ID for the container process.
    #[serde(rename = "pid", skip_serializing_if = "Option::is_none")]
    pub pid: Option<i32>,
    /// Bundle is the path to the container's bundle directory.
    #[serde(rename = "bundle")]
    pub bundle: String,
    /// Annotations are key values associated with the container.
    #[serde(rename = "annotations", skip_serializing_if = "Option::is_none")]
    pub annotations: Option<HashMap<String, String>>,
}
