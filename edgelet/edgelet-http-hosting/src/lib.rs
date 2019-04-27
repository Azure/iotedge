// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]

pub mod client;
pub mod error;

pub use client::{HostingClient, HostingInterface};
pub use error::{Error, ErrorKind};

pub const HOSTING_API_VERSION: &str = "2019-04-10";
