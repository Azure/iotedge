// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate edgelet_core;
#[macro_use]
extern crate failure;
extern crate futures;
#[cfg(windows)]
extern crate httparse;
extern crate hyper;
#[cfg(unix)]
extern crate hyperlocal;
#[cfg(windows)]
extern crate mio;
#[cfg(windows)]
extern crate mio_named_pipes;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate tokio_io;

use std::net::TcpListener;

pub mod cert;
pub mod identity;
mod json_connector;
pub mod module;
pub mod web;

pub use json_connector::{JsonConnector, StaticStream};
pub use web::run_tcp_server;

#[cfg(unix)]
pub use web::run_uds_server;

#[cfg(windows)]
pub use web::run_pipe_server;

pub fn get_unused_tcp_port() -> u16 {
    TcpListener::bind("127.0.0.1:0")
        .unwrap()
        .local_addr()
        .unwrap()
        .port()
}
