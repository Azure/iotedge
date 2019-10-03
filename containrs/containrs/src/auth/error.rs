use failure::{Context, Fail};

use www_authenticate::ChallengeScheme;

use crate::error::*;

#[derive(Debug, Fail)]
pub enum AuthError {
    #[fail(display = "Could not reach API endpoint")]
    EndpointNoResponse,

    #[fail(display = "API Endpoint response is missing www-authenticate header")]
    EndpointMissingWWWAuth,

    #[fail(display = "API Endpoint response has malformed www-authenticate header")]
    EndpointMalformedWWWAuth,

    #[fail(
        display = "Server is using an unimplemented authentication scheme \"{:?}\"",
        _0
    )]
    UnimplementedChallengeScheme(ChallengeScheme),

    #[fail(display = "Invalid Credentials")]
    InvalidCredentials,

    #[fail(display = "Could not construct auth server URI from www-authenticate header")]
    AuthServerUri,

    #[fail(display = "Could not reach auth server endpoint")]
    AuthServerNoResponse,

    #[fail(
        display = "Auth server returned an error (status code: {}). See debug logs for response contents.",
        _0
    )]
    AuthServerError(reqwest::StatusCode),

    #[fail(display = "Could not parse auth server response")]
    AuthServerInvalidResponse,

    #[fail(display = "Auth server response is missing a token field")]
    AuthServerMissingToken,

    #[fail(display = "Auth server token is invalid")]
    AuthServerInvalidToken,
}

impl From<AuthError> for Error {
    fn from(e: AuthError) -> Self {
        ErrorKind::Auth(e).into()
    }
}

impl From<Context<AuthError>> for Error {
    fn from(inner: Context<AuthError>) -> Self {
        inner.map(ErrorKind::Auth).into()
    }
}
