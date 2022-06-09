// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::doc_markdown, // clippy want the "IoT" of "IoT Hub" in a code fence
    clippy::missing_errors_doc,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::shadow_unrelated,
    clippy::similar_names,
    clippy::too_many_lines,
    clippy::use_self
)]

pub mod client;
pub mod error;

pub use self::client::IdentityClient;
pub use self::error::{Error, ErrorKind};
