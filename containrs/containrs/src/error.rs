use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};
use serde::{Deserialize, Serialize};

use crate::auth::AuthError;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    // User Input Errors
    #[fail(display = "Could not parse registry URI")]
    ClientRegistryUrlMalformed,

    #[fail(display = "Registry URI is is missing authority (e.g: registry-1.docker.io)")]
    ClientRegistryUrlMissingAuthority,

    #[fail(display = "Registry URI includes query parameters")]
    ClientRegistryUrlIncludesQuery,

    #[fail(display = "Invalid Range")]
    InvalidRange,

    #[fail(display = "Could not construct API Endpoint request")]
    InvalidApiEndpoint,

    #[fail(display = "Invalid Descriptor URL")]
    InvalidDescriptorUrl,

    // API communication
    #[fail(display = "Could not send HTTP request")]
    ClientRequest,

    #[fail(display = "API returned an error (status code: {})", _0)]
    ApiError(reqwest::StatusCode),

    #[fail(display = "API returned malformed Body")]
    ApiMalformedBody,

    #[fail(display = "API returned malformed JSON")]
    ApiMalformedJSON,

    #[fail(display = "API returned malformed pagination link")]
    ApiPaginationLink,

    #[fail(display = "API response is missing a required header: {}", _0)]
    ApiMissingHeader(&'static str),

    #[fail(display = "API response returned a malformed required header: {}", _0)]
    ApiMalformedHeader(&'static str),

    #[fail(display = "API returned a mismatched Docker-Content-Digest header")]
    ApiMismatchedDigest,

    #[fail(display = "API returned a blob with an unexpected media type")]
    ApiMismatchedBlobMediaType,

    #[fail(display = "API returned a blob with an unexpected size")]
    ApiMismatchedBlobSize,

    #[fail(
        display = "API returned an out of spec response (status code: {}). See debug logs for response contents.",
        _0
    )]
    ApiUnexpectedStatus(reqwest::StatusCode),

    #[fail(display = "API doesn't support HTTP Range Headers")]
    ApiRangeHeaderNotSupported,

    // Authentication-related Errors
    #[fail(display = "Auth Flow Error: {}", _0)]
    Auth(AuthError),
}

impl Fail for Error {
    fn cause(&self) -> Option<&dyn Fail> {
        self.inner.cause()
    }

    fn backtrace(&self) -> Option<&Backtrace> {
        self.inner.backtrace()
    }
}

impl Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        Display::fmt(&self.inner, f)
    }
}

impl Error {
    pub fn new(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }

    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
    }
}

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Self {
        Error {
            inner: Context::new(kind),
        }
    }
}

impl From<Context<ErrorKind>> for Error {
    fn from(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }
}

impl From<reqwest::Error> for Error {
    fn from(e: reqwest::Error) -> Self {
        e.context(ErrorKind::ClientRequest).into()
    }
}

/// Wrapper around [oci_distribution::v2::Errors] that implements [Fail].
/// Typically used in conjunction with [ErrorKind::ApiError]
#[derive(Debug, Fail, Serialize, Deserialize)]
pub struct ApiErrors(oci_distribution::v2::Errors);

impl Display for ApiErrors {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{:?}", self.0)
    }
}
