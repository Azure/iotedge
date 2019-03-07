// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::type_complexity,
    clippy::use_self
)]

use futures::Future;
use serde_derive::Deserialize;

mod check;
mod error;
mod list;
mod logs;
mod restart;
mod unknown;
mod version;

pub use crate::check::Check;
pub use crate::error::{Error, ErrorKind, FetchLatestVersionsReason};
pub use crate::list::List;
pub use crate::logs::Logs;
pub use crate::restart::Restart;
pub use crate::unknown::Unknown;
pub use crate::version::Version;

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
