// Copyright (c) Microsoft. All rights reserved.

use failure::{Backtrace, Context, Fail};
use std::fmt::{self, Display};

use crate::IntoResponse;
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Response, StatusCode};
use keyservice::models::ErrorResponse;
use log::error;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Clone, Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Could not start key service")]
    StartService,

    #[fail(display = "Could not get signature")]
    GetSignature,

    #[fail(display = "Request body is malformed")]
    MalformedRequestBody,

    #[fail(display = "Device key not found")]
    DeviceKeyNotFound,

    #[fail(display = "Invalid signature algorithm")]
    InvalidSignatureAlgorithm,
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
            ErrorKind::MalformedRequestBody | ErrorKind::InvalidSignatureAlgorithm => {
                StatusCode::BAD_REQUEST
            }
            ErrorKind::DeviceKeyNotFound => StatusCode::NOT_FOUND,
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
