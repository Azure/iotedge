// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]

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
extern crate url;

extern crate edgelet_utils;

use std::path::{Path, PathBuf};

use failure::ResultExt;
use url::Url;

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
        .unwrap_or_else(|| include_str!("../../version.txt").to_string());
}

pub fn version() -> &'static str {
    &VERSION
}

pub trait UrlExt {
    fn to_uds_file_path(&self) -> Result<PathBuf, Error>;
    fn to_base_path(&self) -> Result<PathBuf, Error>;
}

impl UrlExt for Url {
    fn to_uds_file_path(&self) -> Result<PathBuf, Error> {
        debug_assert_eq!(self.scheme(), UNIX_SCHEME);

        if cfg!(windows) {
            // We get better handling of Windows file syntax if we parse a
            // unix:// URL as a file:// URL. Specifically:
            // - On Unix, `Url::parse("unix:///path")?.to_file_path()` succeeds and
            //   returns "/path".
            // - On Windows, `Url::parse("unix:///C:/path")?.to_file_path()` fails
            //   with Err(()).
            // - On Windows, `Url::parse("file:///C:/path")?.to_file_path()` succeeds
            //   and returns "C:\\path".
            debug_assert_eq!(self.scheme(), UNIX_SCHEME);
            let mut s = self.to_string();
            s.replace_range(..4, "file");
            let url = Url::parse(&s).with_context(|_| ErrorKind::InvalidUrl(s.clone()))?;
            let path = url
                .to_file_path()
                .map_err(|()| ErrorKind::InvalidUrl(url.to_string()))?;
            Ok(path)
        } else {
            Ok(Path::new(self.path()).to_path_buf())
        }
    }

    fn to_base_path(&self) -> Result<PathBuf, Error> {
        match self.scheme() {
            "unix" => Ok(self.to_uds_file_path()?),
            _ => Ok(self.as_str().into()),
        }
    }
}

pub const UNIX_SCHEME: &str = "unix";
