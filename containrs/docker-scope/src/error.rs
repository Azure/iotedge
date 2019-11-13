use std::error::Error as StdError;
use std::fmt;

use pest::error::Error as PestError;

#[derive(Debug)]
pub enum Error {
    Parse(PestError<super::Rule>),
}

impl StdError for Error {}
impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        use self::Error::*;
        match self {
            Parse(e) => write!(f, "Failed to parse docker scope {}", e),
        }
    }
}
