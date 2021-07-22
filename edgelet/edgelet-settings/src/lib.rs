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
pub use base::RuntimeSettings;
pub use base::{aziot, module, uri, watchdog};

#[cfg(feature = "settings-docker")]
pub mod docker;
#[cfg(feature = "settings-docker")]
pub use crate::docker::{
    config::DockerConfig,
    network::{Ipam, MobyNetwork},
    runtime::ContentTrust,
    Settings,
};
