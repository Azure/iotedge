// Copyright (c) Microsoft. All rights reserved.

use std::fmt::{self, Display};

use edgelet_core::Error as CoreError;
use edgelet_iothub::Error as IoTHubError;
use failure::{Backtrace, Context, Fail};
use http::{Error as HttpError, Response, StatusCode};
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Error as HyperError, StatusCode as HyperStatusCode};
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
    #[fail(display = "Http error")]
    Http,
    #[fail(display = "Bad parameter")]
    BadParam,
    #[fail(display = "Bad body")]
    BadBody,
    #[fail(display = "IoT Hub error")]
    IoTHub,
    #[fail(display = "Invalid or missing API version")]
    InvalidApiVersion,
    #[fail(display = "Client error")]
    Client(MgmtError<serde_json::Value>),
    #[fail(display = "State not modified")]
    NotModified,
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

impl From<HttpError> for Error {
    fn from(error: HttpError) -> Error {
        Error {
            inner: error.context(ErrorKind::Http),
        }
    }
}

impl From<MgmtError<serde_json::Value>> for Error {
    fn from(error: MgmtError<serde_json::Value>) -> Error {
        match error {
            MgmtError::Hyper(h) => From::from(h),
            MgmtError::Serde(s) => From::from(s),
            MgmtError::ApiError(ref e) if e.code == HyperStatusCode::NotModified => {
                From::from(ErrorKind::NotModified)
            }
            MgmtError::ApiError(_) => From::from(ErrorKind::Client(error)),
        }
    }
}

impl From<IoTHubError> for Error {
    fn from(error: IoTHubError) -> Error {
        Error {
            inner: error.context(ErrorKind::IoTHub),
        }
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
            ErrorKind::BadParam => StatusCode::BAD_REQUEST,
            ErrorKind::BadBody => StatusCode::BAD_REQUEST,
            ErrorKind::InvalidApiVersion => StatusCode::BAD_REQUEST,
            _ => StatusCode::INTERNAL_SERVER_ERROR,
        };

        let body = serde_json::to_string(&ErrorResponse::new(message))
            .expect("serialization of ErrorResponse failed.");

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

impl IntoResponse for HyperError {
    fn into_response(self) -> Response<Body> {
        Error::from(self).into_response()
    }
}

impl IntoResponse for HttpError {
    fn into_response(self) -> Response<Body> {
        Error::from(self).into_response()
    }
}

impl IntoResponse for IoTHubError {
    fn into_response(self) -> Response<Body> {
        Error::from(self).into_response()
    }
}
