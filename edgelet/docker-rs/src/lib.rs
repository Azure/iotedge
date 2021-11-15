// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![allow(
    dead_code,
    non_snake_case,
    renamed_and_removed_lints,
    unused_imports,
    unused_mut
)]
#![allow(clippy::all, clippy::pedantic)]
#![cfg(not(tarpaulin_include))]
pub mod apis;
pub mod models;
pub mod utils;

pub use apis::{DockerApi, DockerApiClient};
