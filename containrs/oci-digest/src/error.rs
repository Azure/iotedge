use failure::Fail;

#[derive(Debug, Fail)]
pub enum DigestParseError {
    #[fail(display = "Unsupported digest algorithm")]
    Unsupported,

    #[fail(display = "Invalid digest format")]
    InvalidFormat,

    #[fail(display = "Invalid digest length")]
    InvalidLength,

    #[fail(display = "Invalid digest hex string: {}", _0)]
    InvalidHex(hex::FromHexError),
}
