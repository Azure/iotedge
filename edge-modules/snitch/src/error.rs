// Copyright (c) Microsoft. All rights reserved.

use std::error::Error as StdError;
use std::fmt;
use std::io::Error as IoError;
use std::num::ParseIntError;
use std::str;

use http::Error as HttpError;
use hyper::{Error as HyperError, StatusCode as HyperStatusCode};
use serde_json::Error as SerdeJsonError;
use tokio::timer::Error as TimerError;
use url::ParseError as ParseUrlError;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub enum Error {
    Io(IoError),
    Env(String),
    ParseInt(ParseIntError),
    ParseUrl(ParseUrlError),
    SerdeJson(SerdeJsonError),
    Hyper(HyperError),
    Http(HttpError),
    Service(HyperStatusCode, String),
    Timer(TimerError),
}

impl StdError for Error {}

impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        write!(f, "{:?}", self)
    }
}

impl From<ParseIntError> for Error {
    fn from(err: ParseIntError) -> Error {
        Error::ParseInt(err)
    }
}

impl From<ParseUrlError> for Error {
    fn from(err: ParseUrlError) -> Error {
        Error::ParseUrl(err)
    }
}

impl From<SerdeJsonError> for Error {
    fn from(err: SerdeJsonError) -> Error {
        Error::SerdeJson(err)
    }
}

impl From<HyperError> for Error {
    fn from(err: HyperError) -> Error {
        Error::Hyper(err)
    }
}

impl From<HttpError> for Error {
    fn from(err: HttpError) -> Error {
        Error::Http(err)
    }
}

impl From<TimerError> for Error {
    fn from(err: TimerError) -> Error {
        Error::Timer(err)
    }
}

impl From<IoError> for Error {
    fn from(err: IoError) -> Error {
        Error::Io(err)
    }
}

impl<'a> From<(HyperStatusCode, &'a [u8])> for Error {
    fn from(err: (HyperStatusCode, &'a [u8])) -> Self {
        let (status_code, msg) = err;
        Error::Service(
            status_code,
            str::from_utf8(msg)
                .unwrap_or_else(|_| "Could not decode error message")
                .to_string(),
        )
    }
}
