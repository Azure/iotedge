// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};
use hyper::{header, Body, Response, StatusCode};
use serde_json::json;

use crate::IntoResponse;
use url::Url;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "The proxy could not start up successfully: {}", _0)]
    Initialize(InitializeErrorReason),

    #[fail(display = "HTTP connection error")]
    Hyper,

    #[fail(
        display = "Could not form well-formed URL by joining {:?} with {:?}",
        _0, _1
    )]
    UrlJoin(Url, String),

    #[fail(display = "Invalid URI to parse: {:?}", _0)]
    Uri(Url),

    #[fail(display = "Invalid HTTP header value {:?}", _0)]
    HeaderValue(String),

    #[cfg(test)]
    #[fail(display = "Error")]
    Generic,
}

#[derive(Clone, Debug, PartialEq)]
pub enum InitializeErrorReason {
    LoadSettings,
    LoadSettingsUnsupportedSchema(String),
    InvalidUrl(Url),
    InvalidUrlWithReason(Url, String),
    ClientConfig,
    ClientConfigReadFile(String),
    Tokio,
}

impl Display for InitializeErrorReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::LoadSettings => write!(f, "Could not load settings"),
            Self::LoadSettingsUnsupportedSchema(x) => write!(f, "Unsupported URL schema: {}", x),
            Self::InvalidUrl(x) => write!(f, "Could not resolve address for given URL: {}", x),
            Self::InvalidUrlWithReason(url, reason) => write!(
                f,
                "Could not resolve address for given URL: {}. {}",
                url, reason
            ),
            Self::ClientConfig => write!(f, "Could not load proxy client config"),
            Self::ClientConfigReadFile(x) => {
                write!(f, "Could not read file for proxy client config: {}", x)
            }
            Self::Tokio => write!(f, "Could not initialize tokio runtime"),
        }
    }
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
