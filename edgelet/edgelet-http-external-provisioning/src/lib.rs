// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_lines,
    clippy::use_self
)]

pub mod client;
pub mod error;

pub use client::{ExternalProvisioningClient, ExternalProvisioningInterface};
pub use error::{Error, ErrorKind};

pub const EXTERNAL_PROVISIONING_API_VERSION: &str = "2019-04-10";
