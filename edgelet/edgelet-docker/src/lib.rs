// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]

mod client;
mod config;
mod error;
mod module;
mod runtime;
mod settings;

pub use crate::config::DockerConfig;
pub use error::{Error, ErrorKind};
pub use module::{DockerModule, MODULE_TYPE};
pub use runtime::DockerModuleRuntime;
pub use settings::{Settings, DEFAULTS};
