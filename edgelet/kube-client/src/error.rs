// Copyright (c) Microsoft. All rights reserved.

use std::env::VarError;
use std::fmt;
use std::fmt::Display;
use std::io::Error as IoError;
use std::num::ParseIntError;

use base64::DecodeError;
use failure::{Backtrace, Context, Fail};
use hyper::header::InvalidHeaderValue;
use hyper::Error as HyperError;
use k8s_openapi::http::uri::InvalidUri;
use k8s_openapi::{RequestError, ResponseError};
use native_tls::Error as NativeTlsError;
use openssl::error::ErrorStack;
use serde_yaml::Error as SerdeYamlError;
use url::ParseError as UrlParseError;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "An IO error occurred.")]
    Io,
    #[fail(display = "A native TLS error occurred.")]
    NativeTls,
    #[fail(display = "An error occurred while reading an environment variable.")]
    EnvVar,
    #[fail(display = "Parse error")]
    Parse,
    #[fail(display = "Serde error")]
    Serde,
    #[fail(display = "Could not locate a kubernetes configuration file.")]
    MissingKubeConfig,
    #[fail(display = "Missing or invalid Kubernetes context in .kube/config file.")]
    MissingOrInvalidKubeContext,
    #[fail(display = "Missing user configuration in .kube/config file.")]
    MissingUser,
    #[fail(display = "Base64 decode error")]
    Base64Decode,
    #[fail(display = "Openssl error")]
    Openssl,
    #[fail(display = "Both file and data missing")]
    MissingData,
    #[fail(display = "Hyper HTTP error")]
    Hyper,
    #[fail(display = "Invalid URI")]
    Uri,
    #[fail(display = "Invalid HTTP header value")]
    HeaderValue,
    #[fail(display = "HTTP request error")]
    Request,
    #[fail(display = "HTTP response error")]
    Response,
    #[cfg(test)]
    #[fail(display = "HTTP test error")]
    HttpTest,
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

impl From<IoError> for Error {
    fn from(error: IoError) -> Self {
        Error {
            inner: error.context(ErrorKind::Io),
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

impl From<VarError> for Error {
    fn from(error: VarError) -> Self {
        Error {
            inner: error.context(ErrorKind::EnvVar),
        }
    }
}

impl From<ParseIntError> for Error {
    fn from(error: ParseIntError) -> Self {
        Error {
            inner: error.context(ErrorKind::Parse),
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

impl From<SerdeYamlError> for Error {
    fn from(error: SerdeYamlError) -> Self {
        Error {
            inner: error.context(ErrorKind::Serde),
        }
    }
}

impl From<DecodeError> for Error {
    fn from(error: DecodeError) -> Self {
        Error {
            inner: error.context(ErrorKind::Base64Decode),
        }
    }
}

impl From<ErrorStack> for Error {
    fn from(error: ErrorStack) -> Self {
        Error {
            inner: error.context(ErrorKind::Openssl),
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

impl From<InvalidUri> for Error {
    fn from(error: InvalidUri) -> Self {
        Error {
            inner: error.context(ErrorKind::Uri),
        }
    }
}

impl From<InvalidHeaderValue> for Error {
    fn from(error: InvalidHeaderValue) -> Self {
        Error {
            inner: error.context(ErrorKind::HeaderValue),
        }
    }
}

impl From<RequestError> for Error {
    fn from(error: RequestError) -> Self {
        Error {
            inner: error.context(ErrorKind::Request),
        }
    }
}

impl From<ResponseError> for Error {
    fn from(error: ResponseError) -> Self {
        Error {
            inner: error.context(ErrorKind::Response),
        }
    }
}

#[cfg(test)]
impl From<&str> for Error {
    fn from(_error: &str) -> Self {
        Error::new(Context::new(ErrorKind::HttpTest))
    }
}
