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
    // Client Construction
    #[fail(display = "Could not parse registry URI")]
    ClientRegistryUriMalformed,

    #[fail(display = "Registry URI is is missing authority (e.g: registry-1.docker.io)")]
    ClientRegistryUriMissingAuthority,

    #[fail(display = "Registry URI includes scheme. Use `scheme` parameter instead.")]
    ClientRegistryUriHasScheme,

    #[fail(display = "Could not parse scheme")]
    ClientMalformedScheme,

    // API communication
    #[fail(display = "Could not construct API Endpoint request")]
    InvalidApiEndpoint,

    #[fail(display = "Could not send authenticated request")]
    AuthClientRequest,

    #[fail(display = "API returned an error (status code: {})", _0)]
    ApiError(hyper::StatusCode),

    #[fail(display = "API returned malformed JSON")]
    ApiMalformedJSON,

    #[fail(
        display = "API returned an out of spec response (status code: {}). See debug logs for response contents.",
        _0
    )]
    ApiUnexpectedStatus(hyper::StatusCode),

    // Authentication-related Errors
    #[fail(display = "Auth Flow Error: {}", _0)]
    Auth(AuthError),

    // Misc
    #[fail(display = "Could not parse hyper::Body as JSON")]
    UtilHyperJSON,

    // Endpoint contexts
    #[fail(display = "Error while fetching _catalog")]
    EndpointCatalog,
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

/// Wrapper around [oci_distribution::v2::Errors] that implements [Fail].
/// Typically used in conjunction with [ErrorKind::ApiError]
#[derive(Debug, Fail, Serialize, Deserialize)]
pub struct ApiErrors(oci_distribution::v2::Errors);

impl Display for ApiErrors {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{:?}", self.0)
    }
}
