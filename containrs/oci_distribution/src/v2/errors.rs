use serde::{Deserialize, Serialize};
use serde_json::Value;

/// Actionable failure conditions, covered in detail in their relevant sections,
/// are reported as part of 4xx responses, in a json response body. One or more
/// errors will be returned in the following format.
#[derive(Debug, Serialize, Deserialize)]
pub struct Errors {
    #[serde(rename = "errors")]
    errors: Vec<Error>,
}

/// Inner Error structure used in [Errors]
#[derive(Debug, Serialize, Deserialize)]
pub struct Error {
    /// The code field will be a unique identifier, all caps with underscores by
    /// convention.
    #[serde(rename = "code")]
    pub code: ErrorCode,
    /// The message field will be a human readable string.
    #[serde(rename = "message")]
    pub message: String,
    /// The OPTIONAL detail field MAY contain arbitrary json data providing
    /// information the client can use to resolve the issue.
    #[serde(rename = "detail", skip_serializing_if = "Option::is_none")]
    pub detail: Option<Value>,
}

// While the client can take action on certain error codes, the registry MAY
// add new error codes over time. All client implementations SHOULD treat
// unknown error codes as UNKNOWN, allowing future error codes to be added
// without breaking API compatibility. For the purposes of the specification
// error codes will only be added and never removed.

/// Error codes used by [Error]
#[derive(Debug, Serialize, Deserialize)]
#[allow(non_camel_case_types)]
pub enum ErrorCode {
    /// This error MAY be returned when a blob is unknown to the registry in a
    /// specified repository. This can be returned with a standard get or if a
    /// manifest references an unknown layer during upload.
    BLOB_UNKNOWN,
    /// The blob upload encountered an error and can no longer proceed.
    BLOB_UPLOAD_INVALID,
    /// If a blob upload has been cancelled or was never started, this error
    /// code MAY be returned.
    BLOB_UPLOAD_UNKNOWN,
    /// When a blob is uploaded, the registry will check that the content
    /// matches the digest provided by the client. The error MAY include a
    /// detail structure with the key "digest", including the invalid digest
    /// string. This error MAY also be returned when a manifest includes an
    /// invalid layer digest.
    DIGEST_INVALID,
    /// This error MAY be returned when a manifest blob is unknown to the
    /// registry.
    MANIFEST_BLOB_UNKNOWN,
    /// During upload, manifests undergo several checks ensuring validity. If
    /// those checks fail, this error MAY be returned, unless a more specific
    /// error is included. The detail will contain information the failed
    /// validation.
    MANIFEST_INVALID,
    /// This error is returned when the manifest, identified by name and tag is
    /// unknown to the repository.
    MANIFEST_UNKNOWN,
    /// During manifest upload, if the manifest fails signature verification,
    /// this error will be returned.
    MANIFEST_UNVERIFIED,
    /// Invalid repository name encountered either during manifest validation or
    /// any API operation.
    NAME_INVALID,
    /// This is returned if the name used during an operation is unknown to the
    /// registry.
    NAME_UNKNOWN,
    /// When a layer is uploaded, the provided size will be checked against the
    /// uploaded content. If they do not match, this error will be returned.
    SIZE_INVALID,
    /// URI  During a manifest upload, if the tag in the manifest does not match
    /// the uri tag, this error will be returned.
    TAG_INVALID,
    /// The access controller was unable to authenticate the client. Often this
    /// will be accompanied by a Www-Authenticate HTTP response header
    /// indicating how to authenticate.
    UNAUTHORIZED,
    /// The access controller denied access for the operation on a resource.
    DENIED,
    /// The operation was unsupported due to a missing implementation or invalid
    /// set of parameters.
    UNSUPPORTED,
    /// Unknown error code
    #[serde(other)]
    UNKNOWN,
}
