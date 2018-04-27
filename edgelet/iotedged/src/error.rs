// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;
use std::io;
use std::net::AddrParseError;

use config::ConfigError as SettingsError;
use edgelet_core::Error as CoreError;
use edgelet_docker::Error as DockerError;
use failure::{Backtrace, Context, Fail};
use hyper::Error as HyperError;
use hyper_tls::Error as HyperTlsError;
use iothubservice::error::Error as IotHubError;
use serde_json::Error as JsonError;
use url::ParseError;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Invalid configuration file")]
    Settings(SettingsError),
    #[fail(display = "Invalid configuration json")]
    Json(JsonError),
    #[fail(display = "Edgelet core error")]
    Core,
    #[fail(display = "An IO error occurred.")]
    Io,
    #[fail(display = "An HTTP server error occurred.")]
    Hyper,
    #[fail(display = "A TLS error occurred.")]
    HyperTls,
    #[fail(display = "A Docker error occurred.")]
    Docker,
    #[fail(display = "An IoT Hub error occurred.")]
    IotHub,
    #[fail(display = "A parse error occurred.")]
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

impl From<SettingsError> for Error {
    fn from(err: SettingsError) -> Error {
        Error::from(ErrorKind::Settings(err))
    }
}

impl From<JsonError> for Error {
    fn from(err: JsonError) -> Error {
        Error::from(ErrorKind::Json(err))
    }
}

impl From<CoreError> for Error {
    fn from(err: CoreError) -> Error {
        Error {
            inner: err.context(ErrorKind::Core),
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

impl From<DockerError> for Error {
    fn from(error: DockerError) -> Error {
        Error {
            inner: error.context(ErrorKind::Docker),
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

impl From<HyperTlsError> for Error {
    fn from(error: HyperTlsError) -> Error {
        Error {
            inner: error.context(ErrorKind::HyperTls),
        }
    }
}

impl From<IotHubError> for Error {
    fn from(error: IotHubError) -> Error {
        Error {
            inner: error.context(ErrorKind::IotHub),
        }
    }
}

impl From<AddrParseError> for Error {
    fn from(error: AddrParseError) -> Error {
        Error {
            inner: error.context(ErrorKind::Parse),
        }
    }
}

impl From<ParseError> for Error {
    fn from(error: ParseError) -> Error {
        Error {
            inner: error.context(ErrorKind::Parse),
        }
    }
}
