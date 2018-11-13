// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]
#![cfg_attr(
    feature = "cargo-clippy",
    allow(stutter, type_complexity, use_self)
)]

extern crate base64;
extern crate chrono;
extern crate edgelet_core;
#[macro_use]
extern crate edgelet_http;
extern crate edgelet_http_mgmt;
#[cfg(test)]
extern crate edgelet_test_utils;
#[macro_use]
extern crate edgelet_utils;
extern crate failure;
#[macro_use]
extern crate failure_derive;
extern crate futures;
extern crate http;
extern crate hyper;
#[macro_use]
extern crate log;
extern crate serde;
extern crate serde_json;
extern crate workload;

use http::Response;
use hyper::Body;

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
