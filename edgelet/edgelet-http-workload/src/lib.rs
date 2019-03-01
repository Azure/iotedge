// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::type_complexity,
    clippy::use_self
)]

extern crate base64;
extern crate chrono;
extern crate edgelet_core;
#[macro_use]
extern crate edgelet_http;
extern crate edgelet_http_mgmt;
#[cfg(test)]
extern crate edgelet_test_utils;
extern crate edgelet_utils;
extern crate failure;
extern crate futures;
extern crate hyper;
#[macro_use]
extern crate log;
extern crate serde;
extern crate serde_json;
extern crate workload;

use hyper::{Body, Response};

mod error;
mod server;

pub use server::WorkloadService;

pub trait IntoResponse {
    fn into_response(self) -> Response<Body>;
}

impl IntoResponse for Response<Body> {
    fn into_response(self) -> Response<Body> {
        self
    }
}
