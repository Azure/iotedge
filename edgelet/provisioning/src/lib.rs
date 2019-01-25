// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]
#![cfg_attr(feature = "cargo-clippy", allow(stutter, use_self))]

extern crate base64;
extern crate bytes;
extern crate failure;
extern crate futures;
extern crate hsm;
#[macro_use]
extern crate log;
extern crate regex;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
#[cfg(test)]
extern crate tempdir;
#[cfg(test)]
extern crate tokio;
extern crate url;

extern crate dps;
extern crate edgelet_core;
extern crate edgelet_hsm;
extern crate edgelet_http;
extern crate edgelet_utils;

pub mod error;
pub mod provisioning;

pub use error::Error;
pub use provisioning::{BackupProvisioning, DpsProvisioning, Provision, ProvisioningResult, DpsSymmetricKeyProvisioning};
