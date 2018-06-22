// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

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
extern crate regex;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate sha2;
extern crate tokio;
extern crate tokio_timer;

#[macro_use]
extern crate edgelet_utils;

mod authorization;
mod certificate_properties;
pub mod crypto;
mod error;
mod identity;
mod module;
pub mod pid;
pub mod watchdog;

pub use authorization::{Authorization, Policy};
pub use certificate_properties::{CertificateProperties, CertificateType};
pub use crypto::{
    Certificate, CreateCertificate, Decrypt, Encrypt, GetTrustBundle, KeyBytes, KeyIdentity,
    KeyStore, MasterEncryptionKey, PrivateKey, Signature,
};
pub use error::{Error, ErrorKind};
pub use identity::{AuthType, Identity, IdentityManager, IdentitySpec};
pub use module::{
    LogOptions, LogTail, Module, ModuleRegistry, ModuleRuntime, ModuleRuntimeState, ModuleSpec,
    ModuleStatus, SystemInfo,
};

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
