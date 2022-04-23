// Copyright (c) Microsoft. All rights reserved.

use std::fmt;

#[derive(Clone, Debug, PartialEq, thiserror::Error)]
pub enum Error {
    #[error("The symmetric key string could not be activated")]
    ActivateSymmetricKey,

    #[error("The certificate management expiration timer encountered a failure.")]
    CertificateExpirationManagement,

    #[error("The device has been de-provisioned")]
    DeviceDeprovisioned,

    #[error("The timer that checks the edge runtime status encountered an error.")]
    EdgeRuntimeStatusCheckerTimer,

    #[error("The daemon could not start up successfully: {0}")]
    Initialize(InitializeErrorReason),

    #[error("Invalid signed token was provided.")]
    InvalidSignedToken,

    #[error("The management service encountered an error")]
    ManagementService,

    #[error("A module runtime error occurred.")]
    ModuleRuntime,

    #[error("The reprovisioning operation failed")]
    ReprovisionFailure,

    #[error("The symmetric key string is malformed")]
    SymmetricKeyMalformed,

    #[error("The watchdog encountered an error")]
    Watchdog,

    #[error("The workload service encountered an error")]
    WorkloadService,

    #[error("The workload manager encountered an error")]
    WorkloadManager,
}

impl From<edgelet_core::Error> for Error {
    fn from(error: edgelet_core::Error) -> Self {
        match error {
            edgelet_core::Error::EdgeRuntimeIdentityNotFound =>
                Error::Initialize(InitializeErrorReason::InvalidDeviceConfig),
            _ => Error::Watchdog,
        }
    }
}

impl From<&Error> for i32 {
    fn from(err: &Error) -> Self {
        match err {
            // Using 150 as the starting base for custom IoT edge error codes so as to avoid
            // collisions with -
            // 1. The standard error codes defined by the BSD ecosystem
            // (https://www.freebsd.org/cgi/man.cgi?query=sysexits&apropos=0&sektion=0&manpath=FreeBSD+11.2-stable&arch=default&format=html)
            // that is recommended by the Rust docs
            // (https://rust-lang-nursery.github.io/cli-wg/in-depth/exit-code.html)
            // 2. Bash scripting exit codes with special meanings
            // (http://www.tldp.org/LDP/abs/html/exitcodes.html)
            Error::Initialize(InitializeErrorReason::InvalidDeviceConfig) => 150,
            Error::Initialize(InitializeErrorReason::InvalidHubConfig) => 151,
            Error::InvalidSignedToken => 152,
            Error::Initialize(InitializeErrorReason::LoadSettings) => 153,
            Error::DeviceDeprovisioned => 154,
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
    SaveProvisioning,
    StopExistingModules,
    Tokio,
    WorkloadService,
    WorkloadManager,
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

            InitializeErrorReason::SaveProvisioning => {
                write!(f, "Could not save provisioning state file")
            }

            InitializeErrorReason::Tokio => write!(f, "Could not initialize tokio runtime"),

            InitializeErrorReason::WorkloadService => write!(f, "Could not start workload service"),

            InitializeErrorReason::WorkloadManager => write!(f, "Could not start workload manager"),
        }
    }
}
