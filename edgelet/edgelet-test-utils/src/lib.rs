// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::similar_names,
    clippy::use_self,
    clippy::too_many_lines
)]

pub mod cert;
pub mod crypto;
pub mod identity;
mod json_connector;
pub mod module;
pub mod web;

pub use crate::json_connector::{JsonConnector, StaticStream};
pub use crate::web::run_tcp_server;
pub use crate::web::run_uds_server;

#[cfg(windows)]
pub use crate::web::run_pipe_server;
