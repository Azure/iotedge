// Copyright (c) Microsoft. All rights reserved.

use std::fmt::{self, Display};
use std::net::SocketAddr;
use std::str;

use failure::{Backtrace, Compat, Context, Fail};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Response, StatusCode, Uri};
use serde_json::json;
use systemd::Fd;
use url::Url;

use crate::IntoResponse;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "An error occurred while authorizing the HTTP request")]
    Authorization,

    #[fail(display = "An error occurred while binding a listener to {}", _0)]
    BindListener(BindListenerType),

    #[fail(display = "Unable to delete a TLS certificate")]
    CertificateDeletionError,

    #[fail(display = "Unable to create a TLS certificate")]
    CertificateCreationError,

    #[fail(display = "Unable to convert a TLS certificate into a PKCS#12 certificate")]
    CertificateConversionError,

    #[fail(display = "Unable to create a certificate expiration timer")]
    CertificateTimerCreationError,

    #[fail(display = "The certificate timer callback failed")]
    CertificateTimerRuntimeError,

    #[fail(display = "A valid certificate was not found")]
    CertificateNotFound,

    #[fail(display = "Could not perform HTTP request")]
    Http,

    #[fail(display = "HTTP request failed: [{}] {}", _0, _1)]
    HttpWithErrorResponse(StatusCode, String),

    #[fail(display = "An error occurred obtaining the client identity certificate")]
    IdentityCertificate,

    #[fail(display = "An error occurred obtaining the client identity private key")]
    IdentityPrivateKey,

    #[fail(display = "Reading identity private key from PEM bytes failed {}", _0)]
    IdentityPrivateKeyRead(String),

    #[fail(display = "Could not initialize")]
    Initialization,

    #[fail(display = "Invalid API version {:?}", _0)]
    InvalidApiVersion(String),

    #[fail(display = "Invalid URL {:?}", _0)]
    InvalidUrl(String),

    #[fail(display = "Invalid URL {:?}: {}", _0, _1)]
    InvalidUrlWithReason(String, InvalidUrlReason),

    #[fail(
        display = "URL parts could not be parsed into a valid URL: scheme: {:?}, base path: {:?}, path: {:?}",
        scheme, base_path, path
    )]
    MalformedUrl {
        scheme: String,
        base_path: String,
        path: String,
    },

    #[fail(display = "Module not found")]
    ModuleNotFound(String),

    #[fail(display = "An error occurred for path {}", _0)]
    Path(String),

    #[fail(display = "An error occurred with the proxy {}", _0)]
    Proxy(Uri),

    #[fail(
        display = "Preparing a PCKS12 client certificate identity failed {}",
        _0
    )]
    PKCS12Identity(String),

    #[fail(display = "An error occurred in the service")]
    ServiceError,

    #[fail(display = "An error occurred configuring the TLS stack")]
    TlsBootstrapError,

    #[fail(display = "An error occurred during creation of the TLS identity from cert")]
    TlsIdentityCreationError,

    #[fail(display = "Token source error")]
    TokenSource,

    #[fail(display = "Could not parse trust bundle")]
    TrustBundle,

    #[fail(
        display = "Could not form well-formed URL by joining {:?} with {:?}",
        _0, _1
    )]
    UrlJoin(Url, String),
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
    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
    }

    pub fn http_with_error_response(status_code: StatusCode, body: &[u8]) -> Self {
        let kind = match str::from_utf8(body) {
            Ok(body) => ErrorKind::HttpWithErrorResponse(status_code, body.to_string()),
            Err(_) => ErrorKind::HttpWithErrorResponse(
                status_code,
                "<could not parse response body as utf-8>".to_string(),
            ),
        };

        kind.into()
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

impl IntoResponse for Error {
    fn into_response(self) -> Response<Body> {
        let mut fail: &dyn Fail = &self;
        let mut message = self.to_string();
        while let Some(cause) = fail.cause() {
            message.push_str(&format!("\n\tcaused by: {}", cause.to_string()));
            fail = cause;
        }

        let status_code = match *self.kind() {
            ErrorKind::Authorization | ErrorKind::ModuleNotFound(_) => StatusCode::NOT_FOUND,
            ErrorKind::InvalidApiVersion(_) => StatusCode::BAD_REQUEST,
            _ => StatusCode::INTERNAL_SERVER_ERROR,
        };

        let body = json!({
            "message": message,
        })
        .to_string();

        Response::builder()
            .status(status_code)
            .header(CONTENT_TYPE, "application/json")
            .header(CONTENT_LENGTH, body.len().to_string().as_str())
            .body(body.into())
            .expect("response builder failure")
    }
}

impl IntoResponse for Compat<Error> {
    fn into_response(self) -> Response<Body> {
        self.into_inner().into_response()
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum BindListenerType {
    Address(SocketAddr),
    Fd(Fd),
}

impl Display for BindListenerType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            BindListenerType::Address(addr) => write!(f, "address {}", addr),
            BindListenerType::Fd(fd) => write!(f, "fd {}", fd),
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum InvalidUrlReason {
    FdNeitherNumberNorName,
    FileNotFound,
    InvalidScheme,
    InvalidCredentials,
    NoAddress,
    NoHost,
    UnrecognizedSocket,
}

impl Display for InvalidUrlReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            InvalidUrlReason::FdNeitherNumberNorName => {
                write!(f, "URL could not be parsed as fd number nor fd name")
            }
            InvalidUrlReason::FileNotFound => write!(f, "Socket file could not be found"),
            InvalidUrlReason::InvalidScheme => write!(f, "URL does not have a recognized scheme"),
            InvalidUrlReason::InvalidCredentials => {
                write!(f, "Username or password could not be parsed from URL")
            }
            InvalidUrlReason::NoAddress => write!(f, "URL has no address"),
            InvalidUrlReason::NoHost => write!(f, "URL has no host"),
            InvalidUrlReason::UnrecognizedSocket => {
                write!(f, "URL does not correspond to a valid socket")
            }
        }
    }
}
