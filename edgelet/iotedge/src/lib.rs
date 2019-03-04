// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::type_complexity,
    clippy::use_self
)]

extern crate bytes;
extern crate chrono;
extern crate chrono_humanize;
#[macro_use]
extern crate clap;
extern crate failure;
#[macro_use]
extern crate futures;
#[cfg(unix)]
extern crate libc;
extern crate regex;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate tabwriter;
extern crate tokio;

extern crate edgelet_config;
extern crate edgelet_core;
extern crate edgelet_docker;
extern crate edgelet_http;

use futures::Future;

mod check;
mod error;
mod list;
mod logs;
mod restart;
mod unknown;
mod version;

pub use check::Check;
pub use error::{Error, ErrorKind, FetchLatestVersionsReason};
pub use list::List;
pub use logs::Logs;
pub use restart::Restart;
pub use unknown::Unknown;
pub use version::Version;

pub trait Command {
    type Future: Future<Item = ()> + Send;

    fn execute(&mut self) -> Self::Future;
}

#[derive(Debug, Deserialize)]
pub struct LatestVersions {
    pub iotedged: String,

    #[serde(rename = "azureiotedge-agent")]
    pub edge_agent: String,

    #[serde(rename = "azureiotedge-hub")]
    pub edge_hub: String,
}
