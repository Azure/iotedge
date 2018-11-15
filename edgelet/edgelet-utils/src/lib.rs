// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]
#![cfg_attr(feature = "cargo-clippy", allow(stutter, use_self))]

#[macro_use]
extern crate failure;
#[cfg(test)]
extern crate futures;
#[macro_use]
extern crate log;
extern crate serde;

// Need serde_derive only for unit tests.
#[cfg(test)]
#[macro_use]
extern crate serde_derive;

// Need macros from serde_json for unit tests.
#[cfg(test)]
#[macro_use]
extern crate serde_json;

// Need stuff other than macros from serde_json for non-test code.
#[cfg(not(test))]
extern crate serde_json;

mod error;
mod logging;
pub mod macros;
mod ser_de;

use std::collections::HashMap;

pub use error::{Error, ErrorKind};
pub use logging::log_failure;
pub use ser_de::{serde_clone, string_or_struct};
pub use macros::ensure_not_empty_with_context;

pub fn parse_query(query: &str) -> HashMap<&str, &str> {
    query
        .split('&')
        .filter_map(|seg| {
            if seg.is_empty() {
                None
            } else {
                let mut tokens = seg.splitn(2, '=');
                if let Some(key) = tokens.next() {
                    let val = tokens.next().unwrap_or("");
                    Some((key, val))
                } else {
                    // if there's no key then we ignore this token
                    None
                }
            }
        }).collect()
}

pub fn prepare_cert_uri_module(hub_name: &str, device_id: &str, module_id: &str) -> String {
    format!(
        "URI: azureiot://{}/devices/{}/module/{}",
        hub_name, device_id, module_id
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_query_empty_input() {
        assert_eq!(0, parse_query("").len());
    }

    #[test]
    fn parse_query_white_space_input() {
        let inp = "     ";
        let map = parse_query(inp);
        assert_eq!(map.get(inp), Some(&""));
    }

    #[test]
    fn parse_query_simple_str_input() {
        let inp = "some_string";
        let map = parse_query(inp);
        assert_eq!(map.get(inp), Some(&""));
    }

    #[test]
    fn parse_query_key_but_no_value_input() {
        let map = parse_query("k1=");
        assert_eq!(map.get("k1"), Some(&""));
    }

    #[test]
    fn parse_query_single_key_value() {
        let map = parse_query("k1=v1");
        assert_eq!(map.get("k1"), Some(&"v1"));
    }

    #[test]
    fn parse_query_multiple_key_value() {
        let map = parse_query("k1=v1&k2=v2");
        assert_eq!(map.get("k1"), Some(&"v1"));
        assert_eq!(map.get("k2"), Some(&"v2"));
    }

    #[test]
    fn parse_query_complex_input() {
        let map = parse_query("k1=10&k2=v2&k3=this%20is%20a%20string&bling&k4=10=20");
        assert_eq!(map.get("k1"), Some(&"10"));
        assert_eq!(map.get("k2"), Some(&"v2"));
        assert_eq!(map.get("k3"), Some(&"this%20is%20a%20string"));
        assert_eq!(map.get("k4"), Some(&"10=20"));
        assert_eq!(map.get("bling"), Some(&""));
    }

    #[test]
    fn validate_cert_uri_module() {
        assert_eq!(
            "URI: azureiot://hub_id/devices/did/module/mid",
            prepare_cert_uri_module("hub_id", "did", "mid")
        );
    }

}
