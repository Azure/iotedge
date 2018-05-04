// Copyright (c) Microsoft. All rights reserved.

use std::fmt::{self, Display};

use failure::{Backtrace, Context, Fail};
use http::{Response, StatusCode};
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Error as HyperError};

use IntoResponse;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Hyper error")]
    Hyper,
    #[fail(display = "Invalid or missing API version")]
    InvalidApiVersion,
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

impl From<HyperError> for Error {
    fn from(error: HyperError) -> Error {
        Error {
            inner: error.context(ErrorKind::Hyper),
        }
    }
}

impl From<Error> for HyperError {
    fn from(_error: Error) -> HyperError {
        HyperError::Method
    }
}

impl IntoResponse for Error {
    fn into_response(self) -> Response<Body> {
        let mut fail: &Fail = &self;
        let mut message = self.to_string();
        while let Some(cause) = fail.cause() {
            message.push_str(&format!("\n\tcaused by: {}", cause.to_string()));
            fail = cause;
        }

        let status_code = match *self.kind() {
            ErrorKind::InvalidApiVersion => StatusCode::BAD_REQUEST,
            _ => StatusCode::INTERNAL_SERVER_ERROR,
        };

        let body = json!({
            "message": message,
        }).to_string();

        Response::builder()
            .status(status_code)
            .header(CONTENT_TYPE, "application/json")
            .header(CONTENT_LENGTH, body.len().to_string().as_str())
            .body(body.into())
            .expect("response builder failure")
    }
}

impl IntoResponse for Context<ErrorKind> {
    fn into_response(self) -> Response<Body> {
        let error: Error = Error::from(self);
        error.into_response()
    }
}
