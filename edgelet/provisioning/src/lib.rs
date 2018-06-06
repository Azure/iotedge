// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate base64;
extern crate bytes;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hsm;
extern crate hyper;
#[macro_use]
extern crate log;
extern crate regex;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
#[cfg(test)]
extern crate tempdir;
#[cfg(test)]
extern crate tokio_core;
extern crate url;

extern crate dps;
extern crate edgelet_core;
extern crate edgelet_hsm;
extern crate edgelet_http;
#[macro_use]
extern crate edgelet_utils;

pub mod error;
pub mod provisioning;

pub use error::Error;
pub use provisioning::{BackupProvisioning, DpsProvisioning, Provision, ProvisioningResult};
