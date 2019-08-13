// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;
use std::io::Error as IoError;

use failure::{Backtrace, Context, Fail};
use hyper::http::uri::InvalidUri;
use hyper::{header, Body, Error as HyperError, Response, StatusCode};
use native_tls::Error as NativeTlsError;
use serde_json::json;
use url::ParseError as UrlParseError;

use crate::IntoResponse;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "Could not load settings")]
    LoadSettings,

    #[fail(display = "Unsupported url schema {:?}", _0)]
    UnsupportedSchema(String),

    #[fail(display = "Could not initialize tokio runtime")]
    Tokio,

    #[fail(display = "Invalid URL {:?}", _0)]
    InvalidUrl(String),

    #[fail(display = "Invalid URL {:?}: {}", _0, _1)]
    InvalidUrlWithReason(String, String),

    #[fail(display = "HTTP connection error")]
    Hyper,

    #[fail(display = "A native TLS error occurred")]
    NativeTls,

    #[fail(display = "Parse error url error")]
    Parse,

    #[fail(display = "Invalid URI to parse")]
    Uri,

    #[fail(display = "Invalid HTTP header value {:?}", _0)]
    HeaderValue(String),

    #[fail(display = "An IO error occurred")]
    Io,

    #[fail(display = "An IO error occurred {:?}", _0)]
    File(String),

    #[fail(display = "Error")]
    Generic,
}

impl Error {
    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
    }
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

impl IntoResponse for Error {
    fn into_response(self) -> Response<Body> {
        let mut fail: &dyn Fail = &self;
        let mut message = self.to_string();
        while let Some(cause) = fail.cause() {
            message.push_str(&format!("\n\tcaused by: {}", cause.to_string()));
            fail = cause;
        }

        let status_code = match *self.kind() {
            ErrorKind::Hyper => StatusCode::BAD_GATEWAY,
            _ => StatusCode::INTERNAL_SERVER_ERROR,
        };

        let body = json!({
            "message": message,
        })
        .to_string();

        Response::builder()
            .status(status_code)
            .header(header::CONTENT_TYPE, "application/json")
            .header(header::CONTENT_LENGTH, body.len().to_string().as_str())
            .body(body.into())
            .expect("response builder failure")
    }
}

impl From<Context<ErrorKind>> for Error {
    fn from(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }
}

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Self {
        Error {
            inner: Context::new(kind),
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

impl From<NativeTlsError> for Error {
    fn from(error: NativeTlsError) -> Self {
        Error {
            inner: error.context(ErrorKind::NativeTls),
        }
    }
}

impl From<UrlParseError> for Error {
    fn from(error: UrlParseError) -> Self {
        Error {
            inner: error.context(ErrorKind::Parse),
        }
    }
}

impl From<InvalidUri> for Error {
    fn from(error: InvalidUri) -> Self {
        Error {
            inner: error.context(ErrorKind::Uri),
        }
    }
}

impl From<IoError> for Error {
    fn from(error: IoError) -> Self {
        Error {
            inner: error.context(ErrorKind::Io),
        }
    }
}
