// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Clone, Copy, Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "Could not backup provisioning result")]
    CouldNotBackup,

    #[fail(display = "Could not restore previous provisioning result")]
    CouldNotRestore,

    #[fail(display = "Could not initialize DPS provisioning client")]
    DpsInitialization,

    #[fail(display = "Failure during external provisioning. {}", _0)]
    ExternalProvisioning(ExternalProvisioningErrorReason),

    #[fail(display = "Could not provision device")]
    Provision,

    #[fail(display = "Could not reprovision device")]
    Reprovision,
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum ExternalProvisioningErrorReason {
    IdentityCertificateNotSpecified,
    IdentityPrivateKeyNotSpecified,
    InvalidAuthenticationType,
    InvalidCredentialSource,
    InvalidSymmetricKey,
    KeyActivation,
    ProvisioningFailure,
    ReprovisioningFailure,
    SymmetricKeyNotSpecified,
}

impl fmt::Display for ExternalProvisioningErrorReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ExternalProvisioningErrorReason::IdentityCertificateNotSpecified => {
                write!(f, "The identity certificate was not specified.")
            }

            ExternalProvisioningErrorReason::IdentityPrivateKeyNotSpecified => {
                write!(f, "The identity private key was not specified.")
            }

            ExternalProvisioningErrorReason::InvalidAuthenticationType => {
                write!(f, "Invalid authentication type specified.")
            }

            ExternalProvisioningErrorReason::InvalidCredentialSource => {
                write!(f, "Invalid credential source specified.")
            }

            ExternalProvisioningErrorReason::InvalidSymmetricKey => {
                write!(f, "Invalid symmetric key specified.")
            }

            ExternalProvisioningErrorReason::KeyActivation => {
                write!(f, "Could not activate symmetric key.")
            }

            ExternalProvisioningErrorReason::ProvisioningFailure => write!(
                f,
                "Error occurred while retrieving device provisioning information."
            ),

            ExternalProvisioningErrorReason::ReprovisioningFailure => {
                write!(f, "Error occurred while reprovisioning the device.")
            }

            ExternalProvisioningErrorReason::SymmetricKeyNotSpecified => {
                write!(f, "Symmetric key not specified.")
            }
        }
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

impl Error {
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
