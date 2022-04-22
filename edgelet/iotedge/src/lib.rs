// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::let_and_return,
    clippy::let_unit_value,
    clippy::missing_errors_doc,
    clippy::missing_panics_doc,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::similar_names,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::type_complexity,
    clippy::use_self
)]

use serde::Deserialize;

mod check;
mod client;
pub mod config;
mod error;
mod internal;
mod list;
mod logs;
mod restart;
mod support_bundle;
mod system;
mod version;

pub use crate::check::{Check, OutputFormat};
pub use crate::client::{MgmtClient, MgmtModule};
pub use crate::error::{Error, FetchLatestVersionsReason};
pub use crate::list::List;
pub use crate::logs::Logs;
pub use crate::restart::Restart;
pub use crate::support_bundle::SupportBundleCommand;
pub use crate::system::System;
pub use crate::version::Version;

#[derive(Debug, Deserialize)]
pub struct LatestVersions {
    #[serde(rename = "aziot-edge")]
    pub aziot_edge: String,
}
