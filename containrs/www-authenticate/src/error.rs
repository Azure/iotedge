use failure::Fail;
use pest::error::Error as PestError;

use super::Rule;

#[derive(Debug, Fail)]
pub enum WWWAuthenticateError {
    #[fail(display = "Failed to parse WWW-Authenticate Header: {}", _0)]
    Pest(PestError<Rule>),

    #[fail(display = "Error in Challenge: {}", _0)]
    BadChallenge(ChallengeError),
}

#[derive(Debug, Fail)]
pub enum ChallengeError {
    #[fail(display = "TODO: add errors")]
    _Placeholder,
}
