// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms)]
#![warn(clippy::all, clippy::pedantic)]
#![allow(
    clippy::missing_errors_doc,
    clippy::missing_panics_doc,
    clippy::must_use_candidate
)]

mod auth;
pub mod error;
mod version;

pub use auth::auth_agent;
pub use auth::auth_caller;

pub use version::ApiVersion;
