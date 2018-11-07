// Copyright (c) Microsoft. All rights reserved.

use std::fmt::{self, Display};
use std::str::ParseBoolError;

use edgelet_core::Error as CoreError;
use edgelet_http::Error as EdgeletHttpError;
use edgelet_iothub::Error as IoTHubError;
use failure::{Backtrace, Context, Fail};
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use http::{Error as HttpError, Response, StatusCode};
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
    #[fail(display = "Parse error")]
    Parse,
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

impl From<CoreError> for Error {
    fn from(error: CoreError) -> Self {
        Error {
            inner: error.context(ErrorKind::Core),
        }
    }
}

impl From<serde_json::Error> for Error {
    fn from(error: serde_json::Error) -> Self {
        Error {
            inner: error.context(ErrorKind::Serde),
        }
    }
}

impl From<HyperError> for Error {
    fn from(error: HyperError) -> Self {
        Error {
            inner: error.context(ErrorKind::Hyper),
        }
    }
}

impl From<HttpError> for Error {
    fn from(error: HttpError) -> Self {
        Error {
            inner: error.context(ErrorKind::Http),
        }
    }
}

impl From<EdgeletHttpError> for Error {
    fn from(error: EdgeletHttpError) -> Self {
        Error {
            inner: error.context(ErrorKind::Http),
        }
    }
}

impl From<MgmtError<serde_json::Value>> for Error {
    fn from(error: MgmtError<serde_json::Value>) -> Self {
        match error {
            MgmtError::Hyper(h) => From::from(h),
            MgmtError::Serde(s) => From::from(s),
            MgmtError::Api(ref e) if e.code == HyperStatusCode::NOT_MODIFIED => {
                From::from(ErrorKind::NotModified)
            }
            MgmtError::Api(_) => From::from(ErrorKind::Client(error)),
        }
    }
}

impl From<IoTHubError> for Error {
    fn from(error: IoTHubError) -> Self {
        Error {
            inner: error.context(ErrorKind::IoTHub),
        }
    }
}

impl From<ParseBoolError> for Error {
    fn from(error: ParseBoolError) -> Self {
        Error {
            inner: error.context(ErrorKind::Parse),
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
            ErrorKind::BadParam | ErrorKind::BadBody | ErrorKind::InvalidApiVersion => {
                StatusCode::BAD_REQUEST
            }
            _ => {
                error!("Internal server error: {}", message);
                StatusCode::INTERNAL_SERVER_ERROR
            }
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
