// Copyright (c) Microsoft. All rights reserved.

use std::fmt::{self, Display};

use base64::DecodeError;
use chrono::format::ParseError;
use edgelet_core::Error as CoreError;
use edgelet_utils::Error as UtilsError;
use failure::{Backtrace, Context, Fail};
use hsm::Error as HsmError;
use hyper::{Error as HyperError, StatusCode};
use hyper::header::{ContentLength, ContentType};
use hyper::server::Response;
use workload::models::ErrorResponse;
use serde_json;

use IntoResponse;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Keystore error")]
    KeyStore,
    #[fail(display = "Serde error")]
    Serde,
    #[fail(display = "Hyper error")]
    Hyper,
    #[fail(display = "Bad parameter")]
    BadParam,
    #[fail(display = "Bad body")]
    BadBody,
    #[fail(display = "Module not found")]
    NotFound,
    #[fail(display = "Sign failed")]
    Sign,
    #[fail(display = "Invalid base64 string")]
    Base64,
    #[fail(display = "Invalid ISO 8601 date")]
    DateParse,
    #[fail(display = "Utils error")]
    Utils,
    #[fail(display = "Hsm error")]
    Hsm,
}

impl Fail for Error {
    fn cause(&self) -> Option<&Fail> {
        self.inner.cause()
    }

    fn backtrace(&self) -> Option<&Backtrace> {
        self.inner.backtrace()
    }
}

impl Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        Display::fmt(&self.inner, f)
    }
}

impl Error {
    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
    }
}

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Error {
        Error {
            inner: Context::new(kind),
        }
    }
}

impl From<Context<ErrorKind>> for Error {
    fn from(inner: Context<ErrorKind>) -> Error {
        Error { inner }
    }
}

impl From<serde_json::Error> for Error {
    fn from(error: serde_json::Error) -> Error {
        Error {
            inner: error.context(ErrorKind::Serde),
        }
    }
}

impl From<HyperError> for Error {
    fn from(error: HyperError) -> Error {
        Error {
            inner: error.context(ErrorKind::Hyper),
        }
    }
}

impl From<DecodeError> for Error {
    fn from(error: DecodeError) -> Error {
        Error {
            inner: error.context(ErrorKind::Base64),
        }
    }
}

impl From<CoreError> for Error {
    fn from(error: CoreError) -> Error {
        Error {
            inner: error.context(ErrorKind::Sign),
        }
    }
}

impl From<ParseError> for Error {
    fn from(error: ParseError) -> Error {
        Error {
            inner: error.context(ErrorKind::DateParse),
        }
    }
}

impl From<UtilsError> for Error {
    fn from(error: UtilsError) -> Error {
        Error {
            inner: error.context(ErrorKind::Utils),
        }
    }
}

impl From<HsmError> for Error {
    fn from(error: HsmError) -> Error {
        Error {
            inner: error.context(ErrorKind::Hsm),
        }
    }
}

impl From<Error> for HyperError {
    fn from(_error: Error) -> HyperError {
        HyperError::Method
    }
}

impl IntoResponse for Error {
    fn into_response(self) -> Response {
        let mut fail: &Fail = &self;
        let mut message = self.to_string();
        while let Some(cause) = fail.cause() {
            message.push_str(&format!("\n\tcaused by: {}", cause.to_string()));
            fail = cause;
        }

        let status_code = match *self.kind() {
            ErrorKind::NotFound => StatusCode::NotFound,
            ErrorKind::BadParam => StatusCode::BadRequest,
            ErrorKind::BadBody => StatusCode::BadRequest,
            ErrorKind::Base64 => StatusCode::UnprocessableEntity,
            _ => StatusCode::InternalServerError,
        };

        // Per the RFC, status code NotModified should not have a body
        let body = if status_code != StatusCode::NotModified {
            let b = serde_json::to_string(&ErrorResponse::new(message))
                .expect("serialization of ErrorResponse failed.");
            Some(b)
        } else {
            None
        };

        body.map(|b| {
            Response::new()
                .with_status(status_code)
                .with_header(ContentLength(b.len() as u64))
                .with_header(ContentType::json())
                .with_body(b)
        }).unwrap_or_else(|| Response::new().with_status(status_code))
    }
}

impl IntoResponse for Context<ErrorKind> {
    fn into_response(self) -> Response {
        let error: Error = Error::from(self);
        error.into_response()
    }
}
