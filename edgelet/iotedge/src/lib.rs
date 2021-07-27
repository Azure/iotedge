// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::let_and_return,
    clippy::let_unit_value,
    clippy::missing_errors_doc,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::similar_names,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::type_complexity,
    clippy::use_self
)]

use futures::Future;
use serde_derive::{Deserialize, Serialize};

mod check;
pub mod config;
mod error;
mod list;
mod logs;
mod restart;
mod support_bundle;
mod system;
mod unknown;
mod version;

pub use crate::check::{Check, OutputFormat};
pub use crate::error::{Error, ErrorKind, FetchLatestVersionsReason};
pub use crate::list::List;
pub use crate::logs::Logs;
pub use crate::restart::Restart;
pub use crate::support_bundle::SupportBundleCommand;
pub use crate::system::System;
pub use crate::unknown::Unknown;
pub use crate::version::Version;

pub trait Command {
    type Future: Future<Item = ()> + Send;

    fn execute(self) -> Self::Future;
}

#[derive(Debug, Default, Deserialize, Serialize)]
pub struct LatestVersions {
    #[serde(rename = "aziot-edge")]
    pub aziot_edge: String,
    #[serde(rename = "azureiotedge-agent")]
    pub aziot_edge_agent: AziotEdgeModuleVersion,
    #[serde(rename = "azureiotedge-hub")]
    pub aziot_edge_hub: AziotEdgeModuleVersion,
}

#[derive(Debug, Default, Deserialize, Serialize)]
pub struct AziotEdgeModuleVersion {
    #[serde(rename = "linux-amd64")]
    pub linux_amd64: DockerImageInfo,
    #[serde(rename = "linux-arm32v7")]
    pub linux_arm32v7: DockerImageInfo,
    #[serde(rename = "linux-arm64v8")]
    pub linux_arm64v8: DockerImageInfo,
}

#[derive(Clone, Debug, Default, Deserialize, Serialize)]
pub struct DockerImageInfo {
    #[serde(rename = "image-tag")]
    pub image_tag: String,
    #[serde(rename = "image-id")]
    pub image_id: String,
}
