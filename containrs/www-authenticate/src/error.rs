use std::error::Error as StdError;
use std::fmt;

use pest::error::Error as PestError;

#[derive(Debug)]
pub enum WWWAuthenticateError {
    Parse(PestError<super::Rule>),
    BadChallenge(ChallengeError),
}

#[derive(Debug)]
pub enum ChallengeError {
    _Placeholder,
}

impl StdError for WWWAuthenticateError {}
impl fmt::Display for WWWAuthenticateError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        use self::WWWAuthenticateError::*;
        match self {
            Parse(e) => write!(f, "Failed to parse WWW-Authenticate Header: {}", e),
            BadChallenge(e) => write!(f, "Error in Challenge: {}", e),
        }
    }
}

impl StdError for ChallengeError {}
impl fmt::Display for ChallengeError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        use self::ChallengeError::*;
        match self {
            _Placeholder => write!(f, "(placeholder)"),
        }
    }
}
