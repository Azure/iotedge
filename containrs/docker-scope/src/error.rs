use failure::Fail;
use pest::error::Error as PestError;

#[derive(Debug, Fail)]
pub enum Error {
    #[fail(display = "Failed to parse docker scope {}", _0)]
    Parse(PestError<super::Rule>),
}
