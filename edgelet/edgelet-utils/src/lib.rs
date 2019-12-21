// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    // clippy want the "IoT" of "IoT Hub" in a code fence
    clippy::doc_markdown,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_lines,
    clippy::use_self
)]

mod error;
mod logging;
pub mod macros;
mod ser_de;
mod yaml_file_source;

use std::collections::HashMap;

pub use crate::error::{Error, ErrorKind};
pub use crate::logging::log_failure;
pub use crate::macros::ensure_not_empty_with_context;
pub use crate::ser_de::{serde_clone, serialize_ordered, string_or_struct};
pub use crate::yaml_file_source::YamlFileSource;

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
        })
        .collect()
}

pub fn prepare_cert_uri_module(hub_name: &str, device_id: &str, module_id: &str) -> String {
    format!(
        "URI: azureiot://{}/devices/{}/modules/{}",
        hub_name, device_id, module_id
    )
}

const ALLOWED_CHAR_DNS: char = '-';
const DNS_MAX_SIZE: usize = 63;

/// The name returned from here must conform to following rules (as per RFC 1035):
///  - length must be <= 63 characters
///  - must be all lower case alphanumeric characters or '-'
///  - must start with an alphabet
///  - must end with an alphanumeric character
pub fn sanitize_dns_label(name: &str) -> String {
    name.trim_start_matches(|c: char| !c.is_ascii_alphabetic())
        .trim_end_matches(|c: char| !c.is_ascii_alphanumeric())
        .to_lowercase()
        .chars()
        .filter(|c| c.is_ascii_alphanumeric() || c == &ALLOWED_CHAR_DNS)
        .take(DNS_MAX_SIZE)
        .collect::<String>()
}

pub fn prepare_dns_san_entries(names: &[&str]) -> String {
    names
        .iter()
        .filter_map(|name| {
            let dns = sanitize_dns_label(name);
            if dns.is_empty() {
                None
            } else {
                Some(format!("DNS:{}", dns))
            }
        })
        .collect::<Vec<String>>()
        .join(", ")
}

pub fn append_dns_san_entries(sans: &str, names: &[&str]) -> String {
    let mut dns_sans = names
        .iter()
        .filter_map(|name| {
            if name.trim().is_empty() {
                None
            } else {
                Some(format!("DNS:{}", name.to_lowercase()))
            }
        })
        .collect::<Vec<String>>()
        .join(", ");
    dns_sans.push_str(", ");
    dns_sans.push_str(sans);
    dns_sans
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
            "URI: azureiot://hub_id/devices/did/modules/mid",
            prepare_cert_uri_module("hub_id", "did", "mid")
        );
    }

    #[test]
    fn dns_label() {
        assert_eq!(
            "abcdefg-hijklmnop-qrs-tuv-wxyz",
            sanitize_dns_label(" -abcdefg-hijklmnop-qrs-tuv-wxyz- ")
        );
        assert!('\u{4eac}'.is_alphanumeric());
        assert_eq!(
            "abcdefg-hijklmnop-qrs-tuv-wxyz",
            sanitize_dns_label("\u{4eac}ABCDEFG-\u{4eac}HIJKLMNOP-QRS-TUV-WXYZ\u{4eac}")
        );
        assert_eq!(String::default(), sanitize_dns_label("--------------"));
        assert_eq!("a", sanitize_dns_label("a"));
        assert_eq!("a-1", sanitize_dns_label("a -  1"));
        assert_eq!("edgehub", sanitize_dns_label("$edgeHub"));
        let expected_name = "a23456789-123456789-123456789-123456789-123456789-123456789-123";
        assert_eq!(expected_name.len(), DNS_MAX_SIZE);
        assert_eq!(
            expected_name,
            sanitize_dns_label("a23456789-123456789-123456789-123456789-123456789-123456789-1234")
        );

        assert_eq!(
            expected_name,
            sanitize_dns_label("$a23456789-123456789-123456789-123456789-123456789-123456789-1234")
        );
    }

    #[test]
    fn dns_san() {
        assert_eq!("DNS:edgehub", prepare_dns_san_entries(&["edgehub"]));
        assert_eq!("DNS:edgehub", prepare_dns_san_entries(&["EDGEhub"]));
        assert_eq!("DNS:edgehub", prepare_dns_san_entries(&["$$$Edgehub"]));
        assert_eq!(
            "DNS:edgehub",
            prepare_dns_san_entries(&["\u{4eac}Edge\u{4eac}hub\u{4eac}"])
        );
        assert_eq!(
            "DNS:edgehub",
            prepare_dns_san_entries(&["$$$Edgehub###$$$"])
        );
        assert_eq!(
            "DNS:edge-hub",
            prepare_dns_san_entries(&["$$$Edge-hub###$$"])
        );
        assert_eq!(
            "DNS:edge-hub",
            prepare_dns_san_entries(&["$$$Ed###ge-h$$^$ub###$$"])
        );

        let name = "$eDgE##-##Hub23212$$$eDgE##-##Hub23212$$$eDgE##-##Hub23212$$$eDgE##-##Hub23212$$$eDgE##-##Hub23212$$";
        let expected_name = "edge-hub23212edge-hub23212edge-hub23212edge-hub23212edge-hub232";
        assert_eq!(
            format!("DNS:{}", expected_name),
            prepare_dns_san_entries(&[name])
        );

        // 63 letters for the name and 4 more for the literal "DNS:"
        assert_eq!(63 + 4, prepare_dns_san_entries(&[name]).len());

        assert_eq!(
            "DNS:edgehub, DNS:edgy",
            prepare_dns_san_entries(&["edgehub", "edgy"])
        );
        assert_eq!(
            "DNS:edgehub, DNS:edgy, DNS:moo",
            prepare_dns_san_entries(&["edgehub", "edgy", "moo"])
        );
        // test skipping invalid entries
        assert_eq!(
            "DNS:edgehub, DNS:moo",
            prepare_dns_san_entries(&[" -edgehub -", "-----", "- moo- "])
        );

        // test appending host name to sanitized label
        let sanitized_labels = prepare_dns_san_entries(&["1edgehub", "2edgy"]);
        assert_eq!(
            "DNS:2019host, DNS:2020host, DNS:edgehub, DNS:edgy",
            append_dns_san_entries(&sanitized_labels, &["2019host", "   ", "2020host"])
        );
    }
}
