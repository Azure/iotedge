// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::bind_instead_of_map,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::type_complexity,
    clippy::use_self
)]

use hyper::{Body, Response};

mod error;
mod server;

pub use crate::server::WorkloadService;

pub trait IntoResponse {
    fn into_response(self) -> Response<Body>;
}
