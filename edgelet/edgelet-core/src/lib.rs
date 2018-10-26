// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]

#[cfg(test)]
extern crate base64;
extern crate bytes;
extern crate chrono;
extern crate consistenttime;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hmac;
#[macro_use]
extern crate lazy_static;
#[macro_use]
extern crate log;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate sha2;
extern crate tokio;

#[macro_use]
extern crate edgelet_utils;

mod authorization;
mod certificate_properties;
pub mod crypto;
mod error;
mod identity;
mod module;
pub mod pid;
pub mod provisioning;
pub mod watchdog;

pub use authorization::{Authorization, Policy};
pub use certificate_properties::{CertificateIssuer, CertificateProperties, CertificateType};
pub use crypto::{
    Certificate, CreateCertificate, Decrypt, Encrypt, GetTrustBundle, KeyBytes, KeyIdentity,
    KeyStore, MasterEncryptionKey, PrivateKey, Signature, IOTEDGED_CA_ALIAS,
};
pub use error::{Error, ErrorKind};
pub use identity::{AuthType, Identity, IdentityManager, IdentitySpec};
pub use module::{
    LogOptions, LogTail, Module, ModuleRegistry, ModuleRuntime, ModuleRuntimeState, ModuleSpec,
    ModuleStatus, SystemInfo,
};
pub use provisioning::{ProvisioningMethod, ProvisioningInfo};

lazy_static! {
    static ref VERSION: String = option_env!("VERSION")
        .map(|version| {
            option_env!("BUILD_SOURCEVERSION")
                .map(|sha| format!("{} ({})", version, sha))
                .unwrap_or_else(|| version.to_string())
        })
        .unwrap_or_else(|| env!("CARGO_PKG_VERSION").to_string());
}

pub fn version() -> &'static str {
    &VERSION
}
