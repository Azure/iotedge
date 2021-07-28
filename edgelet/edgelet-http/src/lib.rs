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
mod list_modules;
mod version;

pub use auth::auth_agent;
pub use auth::auth_caller;

// The list_modules API is used by both management and workload APIs.
pub use list_modules::ListModulesResponse;

pub use version::ApiVersion;

/// Search a query string for the provided key.
pub fn find_query(
    key: &str,
    query: &[(std::borrow::Cow<'_, str>, std::borrow::Cow<'_, str>)],
) -> Option<String> {
    query.iter().find_map(|q| {
        if q.0 == key {
            let value = percent_encoding::percent_decode_str(&q.1)
                .decode_utf8()
                .ok()?
                .to_string();

            Some(value)
        } else {
            None
        }
    })
}
