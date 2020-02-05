use std::error::Error as StdError;
use std::fmt;

#[derive(Debug)]
pub enum DigestParseError {
    Unsupported,
    InvalidFormat,
    InvalidLength,
    InvalidHex(hex::FromHexError),
}

impl StdError for DigestParseError {}
impl fmt::Display for DigestParseError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        use self::DigestParseError::*;
        match self {
            Unsupported => write!(f, "Unsupported digest algorithm"),
            InvalidFormat => write!(f, "Invalid digest format"),
            InvalidLength => write!(f, "Invalid digest length"),
            InvalidHex(e) => write!(f, "Invalid digest hex string: {}", e),
        }
    }
}
