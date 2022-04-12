// Copyright (c) Microsoft. All rights reserved.

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug, thiserror::Error)]
pub enum Error {
    // Only used by edgelet-test-utils
    #[cfg(test)]
    #[error("Identity error")]
    Certificate,

    #[error("An error occurred obtaining the certificate contents")]
    CertificateContent,

    #[error("An error occurred creating the certificate")]
    CertificateCreate,

    #[error("An error occurred destroying the certificate")]
    CertificateDestroy,

    #[error("An error occurred obtaining the certificate's details")]
    CertificateDetail,

    #[error("An error occurred getting the certificate")]
    CertificateGet,

    #[error("An error occurred obtaining the certificate's key")]
    CertificateKey,

    #[error("An error occurred when obtaining the device identity certificate.")]
    DeviceIdentityCertificate,

    #[error("An error occurred when signing using the device identity private key.")]
    DeviceIdentitySign,

    #[error(
        "Edge runtime module has not been created in IoT Hub. Please make sure this device is an IoT Edge capable device."
    )]
    EdgeRuntimeIdentityNotFound,

    #[error("The timer that checks the edge runtime status encountered an error.")]
    EdgeRuntimeStatusCheckerTimer,

    #[error("Unable to get the virtualization status.")]
    GetVirtualizationStatus,

    #[error("An error occurred when obtaining the HSM version")]
    HsmVersion,

    #[error("An identity manager error occurred.")]
    IdentityManager,

    #[error("Invalid image pull policy configuration {0:?}")]
    InvalidImagePullPolicy(String),

    #[error("Invalid or unsupported certificate issuer.")]
    InvalidIssuer,

    #[error("Invalid log tail {0:?}")]
    InvalidLogTail(String),

    #[error("Invalid module name {0:?}")]
    InvalidModuleName(String),

    #[error("Invalid module type {0:?}")]
    InvalidModuleType(String),

    #[error("Invalid URL {0:?}")]
    InvalidUrl(String),

    #[error("An error occurred in the key store.")]
    KeyStore,

    #[error("Item not found.")]
    KeyStoreItemNotFound,

    #[error("An error occured when generating a random number.")]
    MakeRandom,

    #[error("A module runtime error occurred.")]
    ModuleRuntime,

    #[error("Unable to parse since.")]
    ParseSince,

    #[error("Signing error occurred.")]
    Sign,

    #[error("Signing error occurred. Invalid key length: {0}")]
    SignInvalidKeyLength(usize),

    #[error("The workload manager encountered an error")]
    WorkloadManager,
}
