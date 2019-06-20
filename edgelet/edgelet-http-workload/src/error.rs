// Copyright (c) Microsoft. All rights reserved.

use std::fmt::{self, Display};

use failure::{Backtrace, Context, Fail};
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Response, StatusCode};
use log::error;
use serde_json;
use workload::models::ErrorResponse;

use crate::IntoResponse;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Clone, Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Certificate has an invalid private key")]
    BadPrivateKey,

    #[fail(display = "{}", _0)]
    CertOperation(CertOperation),

    #[fail(display = "{}", _0)]
    EncryptionOperation(EncryptionOperation),

    #[fail(display = "Request body is malformed")]
    MalformedRequestBody,

    #[fail(display = "The request parameter `{}` is malformed", _0)]
    MalformedRequestParameter(&'static str),

    #[fail(display = "The request is missing required parameter `{}`", _0)]
    MissingRequiredParameter(&'static str),

    #[fail(display = "Module not found")]
    ModuleNotFound(String),

    #[fail(display = "Could not start workload service")]
    StartService,
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
            ErrorKind::ModuleNotFound(_) => StatusCode::NOT_FOUND,
            ErrorKind::MalformedRequestBody
            | ErrorKind::MalformedRequestParameter(_)
            | ErrorKind::MissingRequiredParameter(_) => StatusCode::BAD_REQUEST,
            _ => {
                error!("Internal server error: {}", message);
                StatusCode::INTERNAL_SERVER_ERROR
            }
        };

        // Per the RFC, status code NotModified should not have a body
        let body = if status_code == StatusCode::NOT_MODIFIED {
            String::new()
        } else {
            serde_json::to_string(&ErrorResponse::new(message))
                .expect("serialization of ErrorResponse failed.")
        };

        let mut response = Response::builder();
        response
            .status(status_code)
            .header(CONTENT_LENGTH, body.len().to_string().as_str());

        if !body.is_empty() {
            response.header(CONTENT_TYPE, "application/json");
        }

        response
            .body(body.into())
            .expect("response builder failure")
    }
}

#[derive(Clone, Copy, Debug)]
pub enum CertOperation {
    CreateIdentityCert,
    GetServerCert,
}

impl fmt::Display for CertOperation {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            CertOperation::CreateIdentityCert => write!(f, "Could not create identity cert"),
            CertOperation::GetServerCert => write!(f, "Could not get server cert"),
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub enum EncryptionOperation {
    Decrypt,
    Encrypt,
    GetTrustBundle,
    Sign,
}

impl fmt::Display for EncryptionOperation {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            EncryptionOperation::Decrypt => write!(f, "Could not decrypt"),
            EncryptionOperation::Encrypt => write!(f, "Could not encrypt"),
            EncryptionOperation::GetTrustBundle => write!(f, "Could not get trust bundle"),
            EncryptionOperation::Sign => write!(f, "Could not sign"),
        }
    }
}
