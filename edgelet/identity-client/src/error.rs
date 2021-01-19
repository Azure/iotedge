// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;
use std::str;

use failure::{Backtrace, Context, Fail};
use hyper::StatusCode;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Connector Uri error")]
    ConnectorUri,

    #[fail(display = "Invalid HTTP header value {:?}", _0)]
    HeaderValue(String),

    #[fail(display = "Hyper HTTP error")]
    Hyper,

    #[fail(display = "Malformed HTTP response")]
    MalformedResponse,

    #[fail(display = "HTTP request error")]
    Request,

    #[fail(display = "HTTP response error: [{}] {}", _0, _1)]
    Response(StatusCode, String),

    #[fail(display = "Serde error: {:?}", _0)]
    Serde(serde_json::Error),

    #[fail(display = "Invalid URI to parse: {:?}", _0)]
    Uri(url::ParseError),
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
    pub fn new(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }

    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
    }

    pub fn http_with_error_response(status_code: StatusCode, body: &[u8]) -> Self {
        let kind = match str::from_utf8(body) {
            Ok(body) => ErrorKind::Response(status_code, body.to_string()),
            Err(_) => ErrorKind::Response(
                status_code,
                "<could not parse response body as utf-8>".to_string(),
            ),
        };

        kind.into()
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
