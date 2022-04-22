// Copyright (c) Microsoft. All rights reserved.

use hyper::StatusCode;

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("Connector Uri error")]
    ConnectorUri,

    #[error("Invalid HTTP header value {0}")]
    HeaderValue(String),

    #[error("Hyper HTTP error")]
    Hyper,

    #[error("Malformed HTTP response")]
    MalformedResponse,

    #[error("HTTP request error")]
    Request,

    #[error("HTTP response error: [{0}] {1}")]
    Response(StatusCode, String),

    #[error("Serde error")]
    Serde,

    #[error("Invalid URI to parse: {0}")]
    Uri(url::ParseError),
}

impl<'a> From<(StatusCode, &'a [u8])> for Error {
    fn from((status_code, body): (StatusCode, &'a [u8])) -> Self {
        Error::Response(
            status_code,
            std::str::from_utf8(body)
                .unwrap_or_else(|_| "<could not parse response body as utf-8>")
                .to_string()
        )
    }
}
