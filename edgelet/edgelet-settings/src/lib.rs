// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms)]
#![warn(clippy::all, clippy::pedantic)]
#![allow(
    clippy::missing_errors_doc,
    clippy::missing_panics_doc,
    clippy::must_use_candidate
)]

pub mod aziot;
pub mod uri;
pub mod watchdog;

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct Settings {
    hostname: String,

    #[serde(skip_serializing_if = "Option::is_none")]
    edge_ca_cert: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    edge_ca_key: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    trust_bundle_cert: Option<String>,

    #[serde(default)]
    auto_reprovisioning_mode: aziot::AutoReprovisioningMode,

    homedir: std::path::PathBuf,

    #[serde(skip_serializing_if = "Option::is_none")]
    manifest_trust_bundle_cert: Option<String>,

    // agent: ModuleSpec<T>,
    connect: uri::Connect,
    listen: uri::Listen,

    #[serde(default)]
    watchdog: watchdog::Settings,

    /// Map of service names to endpoint URIs.
    ///
    /// Only configurable in debug builds for the sake of tests.
    #[serde(default, skip_serializing)]
    #[cfg_attr(not(debug_assertions), serde(skip_deserializing))]
    endpoints: aziot::Endpoints,
}
