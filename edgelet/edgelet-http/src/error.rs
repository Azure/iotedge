// Copyright (c) Microsoft. All rights reserved.

use std::fmt::{self, Display};
use std::io;
use std::num::ParseIntError;
use std::str;
use std::str::Utf8Error;

use edgelet_core::{Error as CoreError, ErrorKind as CoreErrorKind};
use failure::{Backtrace, Context, Fail};
use http::header::{CONTENT_LENGTH, CONTENT_TYPE};
use http::{self, Response, StatusCode};
use hyper::{Body, Error as HyperError, StatusCode as HyperStatusCode};
#[cfg(windows)]
use hyper_named_pipe::Error as PipeError;
use hyper_tls::Error as HyperTlsError;
#[cfg(unix)]
use nix::Error as NixError;
use serde_json::Error as SerdeError;
use systemd::Error as SystemdError;
use typed_headers::Error as TypedHeadersError;
use url::ParseError;

use edgelet_utils::Error as UtilsError;

use IntoResponse;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "IO error")]
    Io,
    #[fail(display = "Service error: [{}] {}", _0, _1)]
    ServiceError(HyperStatusCode, String),
    #[fail(display = "Http error")]
    Http,
    #[fail(display = "Hyper error")]
    Hyper,
    #[fail(display = "Utils error")]
    Utils,
    #[fail(display = "Url parse error")]
    Parse,
    #[fail(display = "Serde error")]
    Serde,
    #[fail(display = "Invalid or missing API version")]
    InvalidApiVersion,
    #[fail(display = "Empty token source")]
    EmptyTokenSource,
    #[fail(display = "Invalid uri {}", _0)]
    InvalidUri(String),
    #[fail(display = "Cannot parse uri")]
    UrlParse,
    #[fail(display = "Token source error")]
    TokenSource,
    #[cfg(windows)]
    #[fail(display = "Named pipe error")]
    HyperPipe,
    #[fail(display = "A TLS error occurred.")]
    HyperTls,
    #[fail(display = "Systemd error")]
    Systemd,
    #[fail(display = "Module not found")]
    NotFound,
    #[cfg(unix)]
    #[fail(display = "Syscall for socket failed.")]
    Nix,
    #[fail(display = "UTF-8 coversion error.")]
    Utf8,
    #[fail(display = "Error creating HTTP header")]
    TypedHeaders,
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

impl From<http::Error> for Error {
    fn from(error: http::Error) -> Error {
        Error {
            inner: error.context(ErrorKind::Http),
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

impl From<Error> for CoreError {
    fn from(_err: Error) -> CoreError {
        CoreError::from(CoreErrorKind::Http)
    }
}

impl From<CoreError> for Error {
    fn from(error: CoreError) -> Error {
        Error {
            inner: error.context(ErrorKind::NotFound),
        }
    }
}

impl From<io::Error> for Error {
    fn from(error: io::Error) -> Error {
        Error {
            inner: error.context(ErrorKind::Io),
        }
    }
}

#[cfg(windows)]
impl From<PipeError> for Error {
    fn from(err: PipeError) -> Error {
        Error {
            inner: err.context(ErrorKind::HyperPipe),
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
            ErrorKind::InvalidApiVersion => StatusCode::BAD_REQUEST,
            ErrorKind::NotFound => StatusCode::NOT_FOUND,
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

impl<'a> From<(HyperStatusCode, &'a [u8])> for Error {
    fn from(err: (HyperStatusCode, &'a [u8])) -> Self {
        let (status_code, msg) = err;
        Error::from(ErrorKind::ServiceError(
            status_code,
            str::from_utf8(msg)
                .unwrap_or_else(|_| "Could not decode error message")
                .to_string(),
        ))
    }
}

impl IntoResponse for Context<ErrorKind> {
    fn into_response(self) -> Response<Body> {
        let error: Error = Error::from(self);
        error.into_response()
    }
}

impl From<ParseError> for Error {
    fn from(error: ParseError) -> Error {
        Error {
            inner: error.context(ErrorKind::Parse),
        }
    }
}

impl From<SerdeError> for Error {
    fn from(error: SerdeError) -> Error {
        Error {
            inner: error.context(ErrorKind::Serde),
        }
    }
}

impl From<UtilsError> for Error {
    fn from(error: UtilsError) -> Error {
        Error {
            inner: error.context(ErrorKind::Utils),
        }
    }
}

impl From<SystemdError> for Error {
    fn from(error: SystemdError) -> Error {
        Error {
            inner: error.context(ErrorKind::Systemd),
        }
    }
}

impl From<ParseIntError> for Error {
    fn from(error: ParseIntError) -> Error {
        Error {
            inner: error.context(ErrorKind::Parse),
        }
    }
}

#[cfg(unix)]
impl From<NixError> for Error {
    fn from(error: NixError) -> Error {
        Error {
            inner: error.context(ErrorKind::Nix),
        }
    }
}

impl From<HyperTlsError> for Error {
    fn from(error: HyperTlsError) -> Error {
        Error {
            inner: error.context(ErrorKind::HyperTls),
        }
    }
}

impl From<Utf8Error> for Error {
    fn from(error: Utf8Error) -> Error {
        Error {
            inner: error.context(ErrorKind::Utf8),
        }
    }
}

impl From<TypedHeadersError> for Error {
    fn from(error: TypedHeadersError) -> Error {
        Error {
            inner: error.context(ErrorKind::TypedHeaders),
        }
    }
}
