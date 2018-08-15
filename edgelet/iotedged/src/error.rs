// Copyright (c) Microsoft. All rights reserved.

use std::env::VarError;
use std::fmt;
use std::fmt::Display;
use std::io;
use std::net::AddrParseError;
#[cfg(target_os = "windows")]
use std::sync::Mutex;

use base64::DecodeError;
use config::ConfigError as SettingsError;
use edgelet_core::Error as CoreError;
use edgelet_docker::Error as DockerError;
use edgelet_hsm::Error as SoftHsmError;
use edgelet_http::Error as HttpError;
use failure::{Backtrace, Context, Fail};
use hsm::Error as HardHsmError;
use hyper::Error as HyperError;
use hyper::error::UriError;
use hyper_tls::Error as HyperTlsError;
use iothubservice::error::Error as IotHubError;
use provisioning::Error as ProvisioningError;
use serde_json::Error as JsonError;
use url::ParseError;
#[cfg(target_os = "windows")]
use windows_service::Error as WindowsServiceError;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail, PartialEq)]
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
    #[cfg(target_os = "windows")]
    #[fail(
        display = "Edge device information is required.\n\
                   Please update the config.yaml and provide the IoTHub connection information.\n\
                   See https://aka.ms/iot-edge-configure-windows for more details."
    )]
    Unconfigured,
    #[cfg(not(target_os = "windows"))]
    #[fail(
        display = "Edge device information is required.\n\
                   Please update the config.yaml and provide the IoTHub connection information.\n\
                   See https://aka.ms/iot-edge-configure-linux for more details"
    )]
    Unconfigured,
    #[fail(display = "A provisioning error occurred.")]
    Provisioning,
    #[fail(display = "A hardware hsm error occurred.")]
    HardHsm,
    #[fail(display = "An hsm error occurred.")]
    SoftHsm,
    #[fail(display = "Env var error")]
    Var,
    #[cfg(target_os = "windows")]
    #[fail(display = "Windows service error")]
    WindowsService,
}

impl Error {
    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
    }
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

// The use of the Mutex below is an artifact of trying to unify 2 different error
// handling crates. `windows_service` uses `error_chain` and we use `failure`.
// `error_chain`'s error type does not implement `Sync` unfortunately (they have
// an open PR to address that). But `failure` requires errors to implement `Sync`.
// So this `Mutex` helps us work around the problem.
#[cfg(target_os = "windows")]
#[derive(Debug, Fail)]
pub struct ServiceError(Mutex<WindowsServiceError>);

#[cfg(target_os = "windows")]
impl Display for ServiceError {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        self.0.lock().unwrap().fmt(f)
    }
}

#[cfg(target_os = "windows")]
impl From<ServiceError> for Error {
    fn from(error: ServiceError) -> Error {
        Error {
            inner: error.context(ErrorKind::WindowsService),
        }
    }
}

#[cfg(target_os = "windows")]
impl From<WindowsServiceError> for Error {
    fn from(error: WindowsServiceError) -> Error {
        Error::from(ServiceError(Mutex::new(error)))
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

impl From<UriError> for Error {
    fn from(error: UriError) -> Error {
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
