// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};

use edgelet_http::{Error as HttpError, ErrorKind as HttpErrorKind};

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Clone, Copy, Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Could not get device registration result")]
    GetDeviceRegistrationResult,

    #[fail(display = "Could not get operation ID")]
    GetOperationId,

    #[fail(display = "Could not get operation status")]
    GetOperationStatus,

    #[fail(display = "Could not get symmetric key attestation operation status")]
    GetOperationStatusForSymmetricKey,

    #[fail(display = "Could not get symmetric challenge key")]
    GetSymmetricChallengeKey,

    #[fail(display = "Could not get token")]
    GetToken,

    #[fail(display = "Could not get TPM challenge key")]
    GetTpmChallengeKey,

    #[fail(display = "Could not get TPM challenge key because the TPM token is invalid")]
    InvalidTpmToken,

    #[fail(display = "DPS registration failed")]
    RegisterWithAuthUnexpectedlyFailed,

    #[fail(display = "DPS registration failed because the DPS operation is not assigned")]
    RegisterWithAuthUnexpectedlyFailedOperationNotAssigned,

    #[fail(display = "DPS registration succeeded but returned an empty response")]
    RegisterWithAuthUnexpectedlySucceeded,

    #[fail(display = "Symmetric key registration failed")]
    RegisterWithSymmetricChallengeKey,
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

impl From<Error> for HttpError {
    fn from(err: Error) -> Self {
        HttpError::from(err.context(HttpErrorKind::TokenSource))
    }
}
