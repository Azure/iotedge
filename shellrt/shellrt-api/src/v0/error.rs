use serde::{Deserialize, Serialize};
use serde::{Deserializer, Serializer};
use serde_json::Value;

/// Describes an error.
///
/// Sent as the output payload when `"status": "err"`
#[derive(Debug, Serialize, Deserialize)]
pub struct Error {
    /// The code field will be a unique identifier corresponding to either a
    /// well-known error, or a plugin-specific error.
    #[serde(
        serialize_with = "error_enum_to_u32",
        deserialize_with = "u32_to_error_enum"
    )]
    pub code: ErrorCode,
    /// The message field will be a human readable string.
    pub message: String,
    /// The OPTIONAL detail field MAY contain arbitrary json data providing
    /// information the client can use to resolve the issue.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub detail: Option<Value>,
}

/// Typed enum corresponding to shellrt error codes.
///
/// Error codes 0-99 are reserved for well-known errors. Values of 100+ can be
/// freely used for plugin specific errors.
#[derive(Debug, Serialize, Deserialize)]
#[allow(non_camel_case_types)]
pub enum ErrorCode {
    /// Incompatible api version
    IncompatibleVersion,
    /// Client passed invalid request
    InvalidRequest,
    /// Unknown error code
    Other(u32),
}

impl From<u32> for ErrorCode {
    fn from(code: u32) -> ErrorCode {
        use self::ErrorCode::*;
        match code {
            1 => IncompatibleVersion,
            2 => InvalidRequest,
            n => Other(n),
        }
    }
}

impl From<&ErrorCode> for u32 {
    fn from(code: &ErrorCode) -> u32 {
        use self::ErrorCode::*;
        match code {
            IncompatibleVersion => 1,
            InvalidRequest => 2,
            Other(n) => *n,
        }
    }
}

impl From<ErrorCode> for u32 {
    fn from(code: ErrorCode) -> u32 {
        (&code).into()
    }
}

fn u32_to_error_enum<'de, D: Deserializer<'de>>(des: D) -> Result<ErrorCode, D::Error> {
    Ok(u32::deserialize(des)?.into())
}

fn error_enum_to_u32<S: Serializer>(v: &ErrorCode, ser: S) -> Result<S::Ok, S::Error> {
    u32::serialize(&v.into(), ser)
}
