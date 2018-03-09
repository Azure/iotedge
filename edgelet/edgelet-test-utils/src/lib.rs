// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate futures;
extern crate hyper;
extern crate serde;
extern crate serde_json;
extern crate tokio_io;

mod json_connector;

pub use json_connector::{JsonConnector, StaticStream};
