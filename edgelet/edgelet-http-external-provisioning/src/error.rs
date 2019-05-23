// Copyright (c) Microsoft. All rights reserved.

use external_provisioning::apis::Error as ExternalProvisioningError;
use failure::{Backtrace, Context, Fail};
use serde_json;
use std::fmt::{self, Display};

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    // Note: This errorkind is always wrapped in another errorkind context
    #[fail(display = "Client error")]
    Client(ExternalProvisioningError<serde_json::Value>),

    #[fail(display = "Could not get device provisioning info")]
    GetDeviceProvisioningInformation,

    #[fail(display = "External provisioning client initialization")]
    InitializeExternalProvisioningClient,
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

    pub fn from_external_provisioning_error(
        error: ExternalProvisioningError<serde_json::Value>,
        context: ErrorKind,
    ) -> Self {
        match error {
            ExternalProvisioningError::Hyper(h) => Error::from(h.context(context)),
            ExternalProvisioningError::Serde(s) => Error::from(s.context(context)),
            ExternalProvisioningError::Api(_) => {
                Error::from(ErrorKind::Client(error).context(context))
            }
        }
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
