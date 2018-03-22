// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate chrono;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
#[cfg(unix)]
extern crate hyperlocal;
extern crate serde;
#[macro_use]
extern crate serde_derive;
// Need stuff other than macros from serde_json for non-test code.
#[cfg(not(test))]
extern crate serde_json;
extern crate url;

// Need macros from serde_json for unit tests.
#[cfg(test)]
#[macro_use]
extern crate serde_json;
#[cfg(test)]
extern crate tempfile;
#[cfg(test)]
extern crate time;

extern crate tokio_core;
extern crate tokio_io;
#[cfg(unix)]
extern crate tokio_uds;

extern crate docker_rs;
extern crate edgelet_core;
#[macro_use]
extern crate edgelet_utils;

#[cfg(test)]
extern crate edgelet_test_utils;

mod config;
mod docker_connector;
mod error;
mod module;
mod runtime;

pub use config::DockerConfig;
pub use error::{Error, ErrorKind};
pub use module::DockerModule;

pub use runtime::DockerModuleRuntime;
