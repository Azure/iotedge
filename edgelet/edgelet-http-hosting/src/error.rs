// Copyright (c) Microsoft. All rights reserved.

use failure::{Backtrace, Context, Fail};
use hosting::apis::Error as HostingError;
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
    Client(HostingError<serde_json::Value>),

    #[fail(display = "Could not get device connection info")]
    GetDeviceConnectionInformation,

    #[fail(display = "Hosting client initialization")]
    InitializeHostingClient,
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

    pub fn from_hosting_error(error: HostingError<serde_json::Value>, context: ErrorKind) -> Self {
        match error {
            HostingError::Hyper(h) => Error::from(h.context(context)),
            HostingError::Serde(s) => Error::from(s.context(context)),
            HostingError::Api(_) => Error::from(ErrorKind::Client(error).context(context)),
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
