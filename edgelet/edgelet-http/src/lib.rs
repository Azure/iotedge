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
mod modules;
mod version;

pub use auth::auth_agent;
pub use auth::auth_caller;

// Common types shared between management and workload APIs.
pub use modules::{ListModulesResponse, ModuleConfig, ModuleDetails};

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

#[cfg(test)]
mod tests {
    macro_rules! cow_tuple_vec {
        ($(($key:expr, $value:expr)),+) => {{
            vec![
                $(
                    (std::borrow::Cow::from($key), std::borrow::Cow::from($value)),
                )+
            ]
        }};
    }

    #[test]
    fn find_query() {
        let query = cow_tuple_vec![("key1", "value1"), ("key2", "value%202")];

        assert_eq!(
            Some("value1".to_string()),
            super::find_query("key1", query.as_slice())
        );

        assert_eq!(
            Some("value 2".to_string()),
            super::find_query("key2", query.as_slice())
        );

        assert_eq!(None, super::find_query("key3", query.as_slice()));
    }
}
