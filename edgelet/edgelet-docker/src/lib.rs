// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate base64;
extern crate chrono;
#[macro_use]
extern crate failure;
#[macro_use]
extern crate futures;
extern crate hyper;
#[macro_use]
extern crate lazy_static;
extern crate serde;
#[macro_use]
extern crate serde_derive;
// Need stuff other than macros from serde_json for non-test code.
#[cfg(not(test))]
extern crate serde_json;
extern crate tokio_core;
extern crate url;

// Need macros from serde_json for unit tests.
#[cfg(test)]
#[macro_use]
extern crate serde_json;
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
