// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::missing_errors_doc,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_lines,
    clippy::use_self
)]

mod client;
mod config;
mod error;
mod module;
mod notary;
mod runtime;
mod settings;

pub use config::{DockerConfig, UPSTREAM_PARENT_KEYWORD};
pub use error::Error;
pub use module::{DockerModule, MODULE_TYPE};
pub use runtime::DockerModuleRuntime;
pub use settings::{ContentTrust, LoadSettingsError, MobyRuntime, Settings, CONFIG_FILE_DEFAULT};
