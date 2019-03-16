// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]

#[cfg(test)]
extern crate chrono;
extern crate edgelet_core;
extern crate edgelet_docker;
#[macro_use]
extern crate edgelet_http;
extern crate edgelet_iothub;
#[cfg(test)]
extern crate edgelet_test_utils;
extern crate failure;
extern crate futures;
extern crate hyper;
#[macro_use]
extern crate lazy_static;
#[macro_use]
extern crate log;
extern crate management;
extern crate serde;
#[cfg(test)]
#[macro_use]
extern crate serde_json;
#[cfg(not(test))]
extern crate serde_json;
extern crate url;

use hyper::{Body, Response};

mod client;
mod error;
mod server;

pub use client::ModuleClient;
pub use error::{Error, ErrorKind};
pub use server::ListModules;
pub use server::ManagementService;

pub trait IntoResponse {
    fn into_response(self) -> Response<Body>;
}

impl IntoResponse for Response<Body> {
    fn into_response(self) -> Response<Body> {
        self
    }
}
