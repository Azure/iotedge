// Copyright (c) Microsoft. All rights reserved.

use std::fmt::{self, Display};
use std::net::SocketAddr;
use std::str;

use hyper::{StatusCode, Uri};
use systemd::Fd;
use url::Url;

#[derive(Debug, PartialEq, thiserror::Error)]
pub enum Error {
    #[error("An error occurred while authorizing the HTTP request")]
    Authorization,

    #[error("An error occurred while binding a listener to {0}")]
    BindListener(BindListenerType),

    #[error("Unable to delete a TLS certificate")]
    CertificateDeletionError,

    #[error("Unable to create a TLS certificate")]
    CertificateCreationError,

    #[error("Unable to convert a TLS certificate into a PKCS#12 certificate")]
    CertificateConversionError,

    #[error("Unable to create a certificate expiration timer")]
    CertificateTimerCreationError,

    #[error("The certificate timer callback failed")]
    CertificateTimerRuntimeError,

    #[error("A valid certificate was not found")]
    CertificateNotFound,

    #[error("Could not perform HTTP request")]
    Http,

    #[error("HTTP request failed: [{0}] {1}")]
    HttpWithErrorResponse(StatusCode, String),

    #[error("An error occurred obtaining the client identity certificate")]
    IdentityCertificate,

    #[error("An error occurred obtaining the client identity private key")]
    IdentityPrivateKey,

    #[error("Reading identity private key from PEM bytes failed")]
    IdentityPrivateKeyRead,

    #[error("Could not initialize")]
    Initialization,

    #[error("Invalid API version {0}")]
    InvalidApiVersion(String),

    #[error("Invalid URL {0}")]
    InvalidUrl(String),

    #[error("Invalid URL {0}: {1}")]
    InvalidUrlWithReason(String, InvalidUrlReason),

    #[error(
        "URL parts could not be parsed into a valid URL: scheme: {scheme}, base path: {base_path}, path: {path}",
    )]
    MalformedUrl {
        scheme: String,
        base_path: String,
        path: String,
    },

    #[error("Module {0} not found")]
    ModuleNotFound(String),

    #[error("An error occurred when binding to the socket path")]
    Path,

    #[error("An error occurred with the proxy {0}")]
    Proxy(Uri),

    #[error(
        "Preparing a PCKS12 client certificate identity failed",
    )]
    PKCS12Identity,

    #[error("An error occurred in the service")]
    ServiceError,

    #[error("An error occurred configuring the TLS stack")]
    TlsBootstrapError,

    #[error("An error occurred during creation of the TLS identity from cert")]
    TlsIdentityCreationError,

    #[error("Token source error")]
    TokenSource,

    #[error("Could not parse trust bundle")]
    TrustBundle,

    #[error(
        "Could not form well-formed URL by joining {0} with {1}",
    )]
    UrlJoin(Url, String),
}

impl Error {
    pub fn http_with_error_response(status_code: StatusCode, body: &[u8]) -> Self {
        match str::from_utf8(body) {
            Ok(body) => Self::HttpWithErrorResponse(status_code, body.to_string()),
            Err(_) => Self::HttpWithErrorResponse(
                status_code,
                "<could not parse response body as utf-8>".to_string(),
            ),
        }
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
