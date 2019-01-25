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
extern crate chrono;
extern crate failure;
extern crate futures;
extern crate hyper;
#[macro_use]
extern crate percent_encoding;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
#[cfg(test)]
extern crate tokio;
#[cfg(test)]
extern crate typed_headers;
#[cfg(test)]
extern crate url;

extern crate edgelet_http;
extern crate edgelet_utils;

mod device;
pub mod error;
mod model;

pub use device::DeviceClient;
pub use error::{Error, ErrorKind, ModuleOperationReason};
pub use model::{AuthMechanism, AuthType, Module, Properties, SymmetricKey, Twin, X509Thumbprint};
