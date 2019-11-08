// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::doc_markdown, // clippy want the "IoT" of "IoT Hub" in a code fence
    clippy::module_name_repetitions,
    clippy::shadow_unrelated,
    clippy::use_self,
    clippy::too_many_lines
)]

pub mod client;
pub mod config;
pub mod error;
pub mod kube;

pub use self::client::{Client, HttpClient};
pub use self::config::{get_config, Config, TokenSource, ValueToken};
pub use self::error::{Error, ErrorKind, RequestType};
