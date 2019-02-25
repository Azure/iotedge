// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::stutter, clippy::use_self)]

#[macro_use]
extern crate lazy_static;
#[macro_use]
extern crate log;

mod authorization;
mod certificate_properties;
pub mod crypto;
mod error;
mod identity;
mod module;
pub mod pid;
pub mod watchdog;
pub mod workload;

pub use authorization::{Authorization, Policy};
pub use certificate_properties::{CertificateIssuer, CertificateProperties, CertificateType};
pub use crypto::{
    Certificate, CreateCertificate, Decrypt, Encrypt, GetTrustBundle, KeyBytes, KeyIdentity,
    KeyStore, MasterEncryptionKey, PrivateKey, Signature, IOTEDGED_CA_ALIAS,
};
pub use error::{Error, ErrorKind};
pub use identity::{AuthType, Identity, IdentityManager, IdentityOperation, IdentitySpec};
pub use module::{
    LogOptions, LogTail, Module, ModuleOperation, ModuleRegistry, ModuleRuntime,
    ModuleRuntimeErrorReason, ModuleRuntimeState, ModuleSpec, ModuleStatus, ModuleTop,
    RegistryOperation, RuntimeOperation, SystemInfo,
};
pub use workload::WorkloadConfig;

lazy_static! {
    static ref VERSION: String = option_env!("VERSION")
        .map(|version| option_env!("BUILD_SOURCEVERSION")
            .map(|sha| format!("{} ({})", version, sha))
            .unwrap_or_else(|| version.to_string()))
        .unwrap_or_else(|| env!("CARGO_PKG_VERSION").to_string());
}

pub fn version() -> &'static str {
    &VERSION
}
