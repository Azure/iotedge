// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::doc_markdown, // clippy wants the "IoT" of "IoT Hub" in a code fence
    clippy::missing_errors_doc,
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

use std::{collections::HashMap, net::IpAddr, str::FromStr};

pub use crate::error::{Error, ErrorKind};
pub use crate::logging::log_failure;
pub use crate::macros::ensure_not_empty_with_context;
pub use crate::ser_de::{serde_clone, string_or_struct};
pub use crate::yaml_file_source::YamlFileSource;

pub fn parse_query(query: &str) -> HashMap<&str, &str> {
    query
        .split('&')
        .filter_map(|seg| {
            if seg.is_empty() {
                None
            } else {
                let mut tokens = seg.splitn(2, '=');
                tokens.next().map(|key| {
                    let val = tokens.next().unwrap_or("");
                    (key, val)
                })
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

pub fn prepare_dns_san_entries<'a>(
    names: impl Iterator<Item = &'a str> + 'a,
) -> impl Iterator<Item = String> + 'a {
    names.filter_map(|name| {
        let dns = sanitize_dns_label(name);
        if dns.is_empty() {
            None
        } else {
            Some(dns)
        }
    })
}

pub fn append_dns_san_entries(sans: &str, names: &[&str]) -> String {
    let mut dns_ip_sans = names
        .iter()
        .filter_map(|name| {
            if IpAddr::from_str(name).is_ok() {
                Some(format!("IP:{}", name))
            } else if name.trim().is_empty() {
                None
            } else {
                Some(name.to_lowercase())
            }
        })
        .collect::<Vec<String>>()
        .join(", ");
    dns_ip_sans.push_str(", ");
    dns_ip_sans.push_str(sans);
    dns_ip_sans
}
