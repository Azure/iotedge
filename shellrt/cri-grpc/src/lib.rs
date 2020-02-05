tonic::include_proto!("runtime.v1alpha2");

/// Constants defined in the CRI spec (as specified in `constants.go` from the
/// `cri-api` repo)
pub mod consts {
    /// RUNTIME_READY means the runtime is up and ready to accept basic
    /// containers.
    pub const RUNTIME_READY: &str = "RuntimeReady";
    /// NETWORK_READY means the runtime network is up and ready to accept
    /// containers which require network.
    pub const NETWORK_READY: &str = "NetworkReady";

    /// LogStreamType is the type of the stream in CRI container log.
    pub type LogStreamType = &'static str;
    /// STDOUT is the stream type for stdout.
    pub const STDOUT: LogStreamType = "stdout";
    /// STDERR is the stream type for stdout.
    pub const STDERR: LogStreamType = "stderr";

    /// LogTag is the tag of a log line in CRI container log.
    /// Currently defined log tags:
    /// * First tag: Partial/Full - P/F.
    ///
    /// The field in the container log format can be extended to include
    /// multiple tags by using a delimiter, but changes should be rare. If
    /// it becomes clear that better extensibility is desired, a more
    /// extensible format (e.g., json) should be adopted as a replacement
    /// and/or addition.
    pub type LogTag = &'static str;
    /// LogTagPartial means the line is part of multiple lines.
    pub const LOG_TAG_PARTIAL: LogTag = "P";
    /// LogTagFull means the line is a single full line or the end of multiple
    /// lines.
    pub const LOG_TAG_FULL: LogTag = "F";
    /// LogTagDelimiter is the delimiter for different log tags.
    pub const LOG_TAG_DELIMITER: &str = ":";
}
