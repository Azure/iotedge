// Copyright (c) Microsoft. All rights reserved.

#![cfg(not(tarpaulin_include))]

pub mod apis;

pub mod models;

pub use apis::{DockerApi, DockerApiClient};
