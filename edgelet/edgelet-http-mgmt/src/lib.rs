// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

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
#[macro_use]
extern crate failure_derive;
extern crate futures;
extern crate http;
extern crate hyper;
#[cfg(test)]
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
extern crate tokio_core;
extern crate url;

use http::Response;
use hyper::Body;

mod client;
mod error;
mod server;

pub use client::ModuleClient;
pub use error::{Error, ErrorKind};
pub use server::ManagementService;

pub trait IntoResponse {
    fn into_response(self) -> Response<Body>;
}

impl IntoResponse for Response<Body> {
    fn into_response(self) -> Response<Body> {
        self
    }
}
