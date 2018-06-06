// Copyright (c) Microsoft. All rights reserved.

use std::env::VarError;
use std::fmt;
use std::fmt::Display;
use std::io;
use std::net::AddrParseError;

use base64::DecodeError;
use config::ConfigError as SettingsError;
use edgelet_core::Error as CoreError;
use edgelet_docker::Error as DockerError;
use edgelet_hsm::Error as SoftHsmError;
use edgelet_http::Error as HttpError;
use failure::{Backtrace, Context, Fail};
use hsm::Error as HardHsmError;
use hyper::Error as HyperError;
use hyper_tls::Error as HyperTlsError;
use iothubservice::error::Error as IotHubError;
use provisioning::Error as ProvisioningError;
use serde_json::Error as JsonError;
use url::ParseError;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Invalid configuration file")]
    Settings,
    #[fail(display = "Invalid configuration json")]
    Json,
    #[fail(display = "Edgelet core error")]
    Core,
    #[fail(display = "Base64 decode error")]
    DecodeError,
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
    #[fail(display = "An http error occurred.")]
    Http,
    #[fail(display = "A provisioning error occurred.")]
    Provisioning,
    #[fail(display = "A hardware hsm error occurred.")]
    HardHsm,
    #[fail(display = "An hsm error occurred.")]
    SoftHsm,
    #[fail(display = "Env var error")]
    Var,
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
    fn from(error: SettingsError) -> Error {
        Error {
            inner: error.context(ErrorKind::Settings),
        }
    }
}

impl From<JsonError> for Error {
    fn from(error: JsonError) -> Error {
        Error {
            inner: error.context(ErrorKind::Json),
        }
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

impl From<DecodeError> for Error {
    fn from(error: DecodeError) -> Error {
        Error {
            inner: error.context(ErrorKind::DecodeError),
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

impl From<HttpError> for Error {
    fn from(error: HttpError) -> Error {
        Error {
            inner: error.context(ErrorKind::Http),
        }
    }
}

impl From<ProvisioningError> for Error {
    fn from(error: ProvisioningError) -> Error {
        Error {
            inner: error.context(ErrorKind::Provisioning),
        }
    }
}

impl From<HardHsmError> for Error {
    fn from(error: HardHsmError) -> Error {
        Error {
            inner: error.context(ErrorKind::HardHsm),
        }
    }
}

impl From<SoftHsmError> for Error {
    fn from(error: SoftHsmError) -> Error {
        Error {
            inner: error.context(ErrorKind::SoftHsm),
        }
    }
}

impl From<VarError> for Error {
    fn from(error: VarError) -> Error {
        Error {
            inner: error.context(ErrorKind::Var),
        }
    }
}
