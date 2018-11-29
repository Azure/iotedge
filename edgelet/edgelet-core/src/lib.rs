// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]
#![cfg_attr(feature = "cargo-clippy", allow(stutter, use_self))]

#[cfg(test)]
extern crate base64;
extern crate bytes;
extern crate chrono;
extern crate consistenttime;
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

extern crate edgelet_utils;

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
    ModuleRuntimeState, ModuleSpec, ModuleStatus, RegistryOperation, RuntimeOperation, SystemInfo,
};
pub use workload::WorkloadConfig;

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
