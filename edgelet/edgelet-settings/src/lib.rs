// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms)]
#![warn(clippy::all, clippy::pedantic)]
#![allow(
    clippy::missing_errors_doc,
    clippy::missing_panics_doc,
    clippy::module_name_repetitions,
    clippy::must_use_candidate
)]

pub mod base;

pub use base::module::Settings as ModuleSpec;
pub use base::{aziot, module, uri, watchdog};
pub use base::{IotedgeMaxRequests, RuntimeSettings};

#[cfg(feature = "settings-docker")]
pub mod docker;
#[cfg(feature = "settings-docker")]
pub use crate::docker::{
    config::{DockerConfig, UPSTREAM_PARENT_KEYWORD},
    network::{Ipam, MobyNetwork},
    runtime::{ContentTrust, MobyRuntime},
    Settings, CONFIG_FILE_DEFAULT,
};

/// ID of the device CA cert in certd and private key in keyd.
pub const AZIOT_EDGED_CA_ALIAS: &str = "aziot-edged-ca";

/// ID of the trust bundle cert in certd.
pub const TRUST_BUNDLE_ALIAS: &str = "aziot-edged-trust-bundle";

/// ID of the trust bundle cert in certd.
pub const MANIFEST_TRUST_BUNDLE_ALIAS: &str = "aziot-edged-manifest-trust-bundle";

/// This is the name of the network created by the aziot-edged
pub const DEFAULT_NETWORKID: &str = "azure-iot-edge";
