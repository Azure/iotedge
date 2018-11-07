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
extern crate chrono;
#[macro_use]
extern crate failure;
#[macro_use]
extern crate futures;
extern crate hyper;
#[macro_use]
extern crate lazy_static;
#[macro_use]
extern crate log;
#[macro_use]
extern crate serde_derive;
#[cfg(test)]
extern crate serde;
// Need stuff other than macros from serde_json for non-test code.
#[cfg(not(test))]
extern crate serde_json;
#[cfg(test)]
extern crate tokio;
extern crate url;

// Need macros from serde_json for unit tests.
#[cfg(test)]
#[macro_use]
extern crate serde_json;
#[cfg(unix)]
#[cfg(test)]
extern crate tempfile;
#[cfg(test)]
extern crate time;

extern crate docker;
extern crate edgelet_core;
extern crate edgelet_http;
#[macro_use]
extern crate edgelet_utils;

#[cfg(test)]
extern crate edgelet_test_utils;

mod client;
mod config;
mod error;
mod module;
mod runtime;

pub use config::DockerConfig;
pub use error::{Error, ErrorKind};
pub use module::{DockerModule, MODULE_TYPE};

pub use runtime::DockerModuleRuntime;
