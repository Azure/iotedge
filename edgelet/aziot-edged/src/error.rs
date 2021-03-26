// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use edgelet_core::Error as CoreError;
use edgelet_core::ErrorKind as CoreErrorKind;

use failure::{Backtrace, Context, Fail};

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Clone, Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "The symmetric key string could not be activated")]
    ActivateSymmetricKey,

    #[fail(display = "The certificate management expiration timer encountered a failure.")]
    CertificateExpirationManagement,

    #[fail(display = "The device has been de-provisioned")]
    DeviceDeprovisioned,

    #[fail(display = "The timer that checks the edge runtime status encountered an error.")]
    EdgeRuntimeStatusCheckerTimer,

    #[fail(display = "The daemon could not start up successfully: {}", _0)]
    Initialize(InitializeErrorReason),

    #[fail(display = "Invalid signed token was provided.")]
    InvalidSignedToken,

    #[fail(display = "The management service encountered an error")]
    ManagementService,

    #[fail(display = "A module runtime error occurred.")]
    ModuleRuntime,

    #[fail(display = "The reprovisioning operation failed")]
    ReprovisionFailure,

    #[fail(display = "The symmetric key string is malformed")]
    SymmetricKeyMalformed,

    #[fail(display = "The watchdog encountered an error")]
    Watchdog,

    #[fail(display = "The workload service encountered an error")]
    WorkloadService,
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

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Self {
        Error {
            inner: Context::new(kind),
        }
    }
}

impl From<CoreError> for Error {
    fn from(error: CoreError) -> Self {
        let error_kind = ErrorKind::Watchdog;

        let error_kind_result = match error.kind() {
            CoreErrorKind::EdgeRuntimeIdentityNotFound => {
                ErrorKind::Initialize(InitializeErrorReason::InvalidDeviceConfig)
            }
            _ => error_kind,
        };

        Error::from(error.context(error_kind_result))
    }
}

impl From<Context<ErrorKind>> for Error {
    fn from(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }
}

impl From<&ErrorKind> for i32 {
    fn from(err: &ErrorKind) -> Self {
        match err {
            // Using 150 as the starting base for custom IoT edge error codes so as to avoid
            // collisions with -
            // 1. The standard error codes defined by the BSD ecosystem
            // (https://www.freebsd.org/cgi/man.cgi?query=sysexits&apropos=0&sektion=0&manpath=FreeBSD+11.2-stable&arch=default&format=html)
            // that is recommended by the Rust docs
            // (https://rust-lang-nursery.github.io/cli-wg/in-depth/exit-code.html)
            // 2. Bash scripting exit codes with special meanings
            // (http://www.tldp.org/LDP/abs/html/exitcodes.html)
            ErrorKind::Initialize(InitializeErrorReason::InvalidDeviceConfig) => 150,
            ErrorKind::Initialize(InitializeErrorReason::InvalidHubConfig) => 151,
            ErrorKind::InvalidSignedToken => 152,
            ErrorKind::Initialize(InitializeErrorReason::LoadSettings) => 153,
            ErrorKind::DeviceDeprovisioned => 154,
            _ => 1,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum InitializeErrorReason {
    CreateCacheDirectory,
    EdgeRuntime,
    GetDeviceInfo,
    IdentityClient,
    InvalidDeviceConfig,
    InvalidHubConfig,
    InvalidIdentityType,
    LoadSettings,
    ManagementService,
    ModuleRuntime,
    RemoveExistingModules,
    StopExistingModules,
    Tokio,
    WorkloadService,
}

impl fmt::Display for InitializeErrorReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            InitializeErrorReason::CreateCacheDirectory => {
                write!(f, "Could not create cache directory")
            }

            InitializeErrorReason::EdgeRuntime => write!(f, "Could not initialize edge runtime"),

            InitializeErrorReason::GetDeviceInfo => {
                write!(f, "Could not retrieve device information")
            }

            InitializeErrorReason::IdentityClient => {
                write!(f, "Could not get device from Identity Service")
            }

            InitializeErrorReason::InvalidDeviceConfig => {
                write!(f, "Invalid device configuration was provided")
            }

            InitializeErrorReason::InvalidHubConfig => {
                write!(f, "Invalid IoT hub configuration was provided")
            }

            InitializeErrorReason::InvalidIdentityType => {
                write!(f, "Invalid identity type was received")
            }

            InitializeErrorReason::LoadSettings => write!(f, "Could not load settings"),

            InitializeErrorReason::ManagementService => {
                write!(f, "Could not start management service")
            }

            InitializeErrorReason::ModuleRuntime => {
                write!(f, "Could not initialize module runtime")
            }

            InitializeErrorReason::RemoveExistingModules => {
                write!(f, "Could not remove existing modules")
            }

            InitializeErrorReason::StopExistingModules => {
                write!(f, "Could not stop existing modules")
            }

            InitializeErrorReason::Tokio => write!(f, "Could not initialize tokio runtime"),

            InitializeErrorReason::WorkloadService => write!(f, "Could not start workload service"),
        }
    }
}
