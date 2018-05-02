// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate edgelet_core;
extern crate failure;
extern crate futures;
extern crate hyper;
#[cfg(unix)]
extern crate hyperlocal;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate tokio_io;

use std::net::TcpListener;

mod json_connector;
pub mod module;
mod web;

pub use json_connector::{JsonConnector, StaticStream};
pub use web::run_tcp_server;
#[cfg(unix)]
pub use web::run_uds_server;

pub fn get_unused_tcp_port() -> u16 {
    TcpListener::bind("127.0.0.1:0")
        .unwrap()
        .local_addr()
        .unwrap()
        .port()
}
