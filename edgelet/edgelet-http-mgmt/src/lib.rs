// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

#[cfg(test)]
extern crate chrono;
extern crate docker_mri;
extern crate edgelet_core;
#[macro_use]
extern crate edgelet_http;
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
extern crate url;

use hyper::Response;

mod error;
mod server;

pub use server::{ApiVersionService, ManagementService, API_VERSION};

pub trait IntoResponse {
    fn into_response(self) -> Response;
}

impl IntoResponse for Response {
    fn into_response(self) -> Response {
        self
    }
}
