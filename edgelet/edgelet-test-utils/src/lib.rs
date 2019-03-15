// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::module_name_repetitions,
    clippy::similar_names,
    clippy::use_self
)]

use std::net::TcpListener;

pub mod cert;
pub mod identity;
mod json_connector;
pub mod module;
pub mod web;

pub use crate::json_connector::{JsonConnector, StaticStream};
pub use crate::web::run_tcp_server;

pub use crate::web::run_uds_server;

#[cfg(windows)]
pub use crate::web::run_pipe_server;

pub fn get_unused_tcp_port() -> u16 {
    TcpListener::bind("127.0.0.1:0")
        .unwrap()
        .local_addr()
        .unwrap()
        .port()
}
