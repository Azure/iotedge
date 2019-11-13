use std::error::Error as StdError;
use std::fmt;

use pest::error::Error as PestError;

use oci_digest::DigestParseError;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub enum Error {
    Parse(PestError<super::Rule>),
    NameTooLong,
    Digest(DigestParseError),
}

impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        use self::Error::*;
        match self {
            Parse(e) => write!(f, "Failed to parse object reference {}", e),
            NameTooLong => write!(f, "Image name is too long (>255 chars)"),
            Digest(e) => write!(f, "Failed to parse Digest: {}", e),
        }
    }
}

impl StdError for Error {}
