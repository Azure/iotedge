// Copyright (c) Microsoft. All rights reserved.

extern crate base64;
#[cfg(test)]
extern crate chrono;
extern crate edgelet_core;
#[macro_use]
extern crate edgelet_http;
extern crate failure;
#[macro_use]
extern crate failure_derive;
extern crate futures;
extern crate hyper;
extern crate log;
extern crate serde;
extern crate serde_json;
extern crate workload;

use hyper::Response;

mod error;
mod server;

pub use server::WorkloadService;

pub trait IntoResponse {
    fn into_response(self) -> Response;
}

impl IntoResponse for Response {
    fn into_response(self) -> Response {
        self
    }
}
