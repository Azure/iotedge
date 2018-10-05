// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![allow(unused_imports, unused_mut, dead_code)]

#[macro_use]
extern crate serde_derive;

#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate serde;
extern crate serde_json;
extern crate typed_headers;
extern crate url;

pub mod apis;
pub mod models;
