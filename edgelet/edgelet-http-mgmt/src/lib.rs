// Copyright (c) Microsoft. All rights reserved.

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
#[macro_use]
extern crate serde_json;

mod error;
mod server;

pub use server::ManagementService;
