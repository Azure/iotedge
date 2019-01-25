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
extern crate chrono;
extern crate failure;
extern crate futures;
#[cfg(test)]
extern crate http;
extern crate hyper;
#[macro_use]
extern crate log;
#[macro_use]
extern crate percent_encoding;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate tokio;
extern crate url;

extern crate edgelet_core;
extern crate edgelet_http;

pub mod error;
mod model;
pub mod registration;

pub use error::{Error, ErrorKind};
pub use model::{
    DeviceRegistration, DeviceRegistrationResult, RegistrationOperationStatus,
    TpmRegistrationResult, DPS_API_VERSION
};
pub use registration::{DpsClient, DpsTokenSource};
