use serde::{Deserialize, Serialize};

/// Return the logs associated with a module
#[derive(Debug, Serialize, Deserialize)]
pub struct LogsRequest {
    /// Module name
    pub name: String,
    /// Keep the stream open, returning new log data as it becomes available
    pub follow: bool,
    /// Only return this number of log lines from the end of the logs.
    pub tail: Option<u32>,
    /// Only return logs since this time (as a unix timestamp)
    pub since: Option<i64>,
}

/// Returned once a LogsRequest is acknowledged
///
/// Once the LogsResponse is sent, the plugin will send a single null byte (to
/// delimit the end of the JSON payload), and begin streaming the unstructured
/// log data over stdout.
#[derive(Debug, Serialize, Deserialize)]
pub struct LogsResponse {}
