// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::stutter, clippy::use_self)]

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
pub use provisioning::{BackupProvisioning, DpsProvisioning, Provision, ProvisioningResult};
