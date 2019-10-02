use failure::Fail;
use pest::error::Error as PestError;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug, Fail)]
pub enum Error {
    #[fail(display = "Failed to parse object reference {}", _0)]
    Parse(PestError<super::Rule>),

    #[fail(display = "Image name is too long (>255 chars)")]
    NameTooLong,
    // TODO: add digest errors
}
