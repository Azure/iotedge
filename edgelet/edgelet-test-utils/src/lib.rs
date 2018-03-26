// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate futures;
extern crate hyper;
#[cfg(unix)]
extern crate hyperlocal;
extern crate serde;
extern crate serde_json;
extern crate tokio_io;

mod web;
mod json_connector;

pub use json_connector::{JsonConnector, StaticStream};
pub use web::run_tcp_server;
#[cfg(unix)]
pub use web::run_uds_server;
