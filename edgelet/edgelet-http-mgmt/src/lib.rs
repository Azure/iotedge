// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

#[cfg(test)]
extern crate chrono;
extern crate edgelet_core;
extern crate edgelet_docker;
#[macro_use]
extern crate edgelet_http;
extern crate edgelet_iothub;
extern crate failure;
#[macro_use]
extern crate failure_derive;
extern crate futures;
extern crate hyper;
#[macro_use]
extern crate log;
extern crate management;
extern crate serde;
#[cfg(test)]
#[macro_use]
extern crate serde_derive;
extern crate serde_json;

use hyper::Response;

mod error;
mod server;

pub use server::ManagementService;

pub trait IntoResponse {
    fn into_response(self) -> Response;
}

impl IntoResponse for Response {
    fn into_response(self) -> Response {
        self
    }
}
