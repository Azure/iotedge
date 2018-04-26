// Copyright (c) Microsoft. All rights reserved.

use std::fmt::{self, Display};

use edgelet_core::Error as CoreError;
use failure::{Backtrace, Context, Fail};
use hyper::{Error as HyperError, StatusCode};
use hyper::header::{ContentLength, ContentType};
use hyper::server::Response;
use serde_json;

use management::apis::Error as MgmtError;
use management::models::ErrorResponse;

use IntoResponse;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Core error")]
    Core,
    #[fail(display = "Module runtime error")]
    ModuleRuntime,
    #[fail(display = "Identity manager error")]
    IdentityManager,
    #[fail(display = "Serde error")]
    Serde,
    #[fail(display = "Hyper error")]
    Hyper,
    #[fail(display = "Bad parameter")]
    BadParam,
    #[fail(display = "Bad body")]
    BadBody,
    #[fail(display = "Invalid or missing API version")]
    InvalidApiVersion,
    #[fail(display = "Client error")]
    Client(MgmtError<serde_json::Value>),
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

impl From<CoreError> for Error {
    fn from(error: CoreError) -> Error {
        Error {
            inner: error.context(ErrorKind::Core),
        }
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

impl From<Error> for HyperError {
    fn from(_error: Error) -> HyperError {
        HyperError::Method
    }
}

impl From<MgmtError<serde_json::Value>> for Error {
    fn from(error: MgmtError<serde_json::Value>) -> Error {
        match error {
            MgmtError::Hyper(h) => From::from(h),
            MgmtError::Serde(s) => From::from(s),
            MgmtError::ApiError(_) => From::from(ErrorKind::Client(error)),
        }
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
            ErrorKind::BadParam => StatusCode::BadRequest,
            ErrorKind::BadBody => StatusCode::BadRequest,
            ErrorKind::InvalidApiVersion => StatusCode::BadRequest,
            _ => StatusCode::InternalServerError,
        };

        let body = serde_json::to_string(&ErrorResponse::new(message))
            .expect("serialization of ErrorResponse failed.");

        Response::new()
            .with_status(status_code)
            .with_header(ContentLength(body.len() as u64))
            .with_header(ContentType::json())
            .with_body(body)
    }
}

impl IntoResponse for Context<ErrorKind> {
    fn into_response(self) -> Response {
        let error: Error = Error::from(self);
        error.into_response()
    }
}

impl IntoResponse for HyperError {
    fn into_response(self) -> Response {
        Error::from(self).into_response()
    }
}
