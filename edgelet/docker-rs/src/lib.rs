// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![allow(
    dead_code,
    non_snake_case,
    renamed_and_removed_lints,
    unused_imports,
    unused_mut
)]
#![allow(clippy::all, clippy::pedantic)]

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
pub mod utils;
