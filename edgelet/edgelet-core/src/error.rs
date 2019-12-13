// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    // Only used by edgelet-test-utils
    #[cfg(test)]
    #[fail(display = "Identity error")]
    Certificate,

    #[fail(display = "An error occurred obtaining the certificate contents")]
    CertificateContent,

    #[fail(display = "An error occurred creating the certificate")]
    CertificateCreate,

    #[fail(display = "An error occurred destroying the certificate")]
    CertificateDestroy,

    #[fail(display = "An error occurred obtaining the certificate's details")]
    CertificateDetail,

    #[fail(display = "An error occurred getting the certificate")]
    CertificateGet,

    #[fail(display = "An error occurred obtaining the certificate's key")]
    CertificateKey,

    #[fail(
        display = "The Connection String is empty. Please update the config.yaml and provide the IoTHub connection information."
    )]
    ConnectionStringEmpty,

    #[fail(display = "The Connection String is missing required parameter {}", _0)]
    ConnectionStringMissingRequiredParameter(&'static str),

    #[fail(
        display = "The Connection String has a malformed value for parameter {}.",
        _0
    )]
    ConnectionStringMalformedParameter(&'static str),

    #[fail(
        display = "Edge device information is required.\nPlease update config.yaml with the IoT Hub connection information.\nSee {} for more details.",
        _0
    )]
    ConnectionStringNotConfigured(&'static str),

    #[fail(display = "An error occurred when obtaining the device identity certificate.")]
    DeviceIdentityCertificate,

    #[fail(display = "An error occurred when signing using the device identity private key.")]
    DeviceIdentitySign,

    #[fail(
        display = "Edge runtime module has not been created in IoT Hub. Please make sure this device is an IoT Edge capable device."
    )]
    EdgeRuntimeIdentityNotFound,

    #[fail(display = "The timer that checks the edge runtime status encountered an error.")]
    EdgeRuntimeStatusCheckerTimer,

    #[fail(display = "An identity manager error occurred.")]
    IdentityManager,

    #[fail(display = "An error occurred when obtaining the HSM version")]
    HsmVersion,

    #[fail(display = "Invalid image pull policy configuration {:?}", _0)]
    InvalidImagePullPolicy(String),

    #[fail(display = "Invalid or unsupported certificate issuer.")]
    InvalidIssuer,

    #[fail(display = "Invalid log tail {:?}", _0)]
    InvalidLogTail(String),

    #[fail(display = "Invalid module name {:?}", _0)]
    InvalidModuleName(String),

    #[fail(display = "Invalid module type {:?}", _0)]
    InvalidModuleType(String),

    #[fail(
        display = "Error parsing URI {} specified for '{}'. Please check the config.yaml file.",
        _0, _1
    )]
    InvalidSettingsUri(String, &'static str),

    #[fail(
        display = "Invalid file URI {} path specified for '{}'. Please check the config.yaml file.",
        _0, _1
    )]
    InvalidSettingsUriFilePath(String, &'static str),

    #[fail(display = "Invalid URL {:?}", _0)]
    InvalidUrl(String),

    #[fail(display = "An error occurred in the key store.")]
    KeyStore,

    #[fail(display = "Item not found.")]
    KeyStoreItemNotFound,

    #[fail(display = "An error occured when generating a random number.")]
    MakeRandom,

    #[fail(display = "A module runtime error occurred.")]
    ModuleRuntime,

    #[fail(display = "Signing error occurred.")]
    Sign,

    #[fail(display = "Signing error occurred. Invalid key length: {}", _0)]
    SignInvalidKeyLength(usize),

    #[fail(
        display = "URI {} is unsupported for '{}'. Please check the config.yaml file.",
        _0, _1
    )]
    UnsupportedSettingsUri(String, &'static str),

    #[fail(
        display = "File URI {} is unsupported for '{}'. Please check the config.yaml file.",
        _0, _1
    )]
    UnsupportedSettingsFileUri(String, &'static str),
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
    pub fn new(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }

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
