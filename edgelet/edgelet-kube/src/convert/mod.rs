// Copyright (c) Microsoft. All rights reserved.

use crate::error::{ErrorKind, Result};
use edgelet_utils::{sanitize_dns_label, sanitize_dns_label_rfc1123};

mod named_secret;
mod to_docker;
mod to_k8s;

pub use named_secret::NamedSecret;
pub use to_docker::pod_to_module;
pub use to_k8s::{
    spec_to_deployment, spec_to_role_binding, spec_to_service_account, trust_bundle_to_config_map,
};

// Services (and consequently module names which are tied to the Service)
// must start with an alphabet.
pub fn sanitize_dns_value(name: &str) -> Result<String> {
    let name_string = sanitize_dns_label(name);
    if name_string.is_empty() {
        Err(ErrorKind::InvalidModuleName(name.to_owned()).into())
    } else {
        Ok(name_string)
    }
}

// Some K8s objects may begin with alphanumerics as per RFC 1123
pub fn sanitize_dns_value_rfc1123(name: &str) -> Result<String> {
    let name_string = sanitize_dns_label_rfc1123(name);
    if name_string.is_empty() {
        Err(ErrorKind::InvalidModuleName(name.to_owned()).into())
    } else {
        Ok(name_string)
    }
}

// Valid label values must be 63 characters or less and must be empty or begin and end with an
// alphanumeric character ([a-z0-9A-Z]) with dashes (-), underscores (_), dots (.), and alphanumerics between.
const LABEL_MAX_SIZE: usize = 63;
fn is_allowed_label_chars(c: char) -> bool {
    c.is_ascii_alphanumeric() || c == '_' || c == '-' || c == '.'
}

pub fn sanitize_label_value(name: &str) -> String {
    // strip both ends so we begin and end in alphanumeric
    let mut trimmed = name
        .trim_matches(|c: char| !c.is_ascii_alphanumeric())
        .to_lowercase();
    // Remove invalid characters and truncate to max length.
    trimmed.retain(is_allowed_label_chars);
    trimmed.truncate(LABEL_MAX_SIZE);
    // if the truncate causes the value to end in non-alphnumeric, trim again.
    if trimmed.ends_with(|c: char| !c.is_ascii_alphanumeric()) {
        trimmed = trimmed
            .trim_end_matches(|c: char| !c.is_ascii_alphanumeric())
            .to_string();
    }
    trimmed
}

#[cfg(test)]
mod tests {
    use super::{sanitize_dns_value, sanitize_label_value, ErrorKind};

    #[test]
    fn sanitize_dns_value_test_63chars() {
        let result = sanitize_dns_value(
            "a1234567890123456789012345678901234567890123456789012345678901234567890123456789",
        )
        .unwrap();
        assert_eq!(
            result,
            "a12345678901234567890123456789012345678901234567890123456789012"
        );
    }
    #[test]
    fn sanitize_dns_value_trim_and_lowercase() {
        let result = sanitize_dns_value("$edgeHub").unwrap();
        assert_eq!(result, "edgehub");
    }
    #[test]
    fn error_from_empty_string() {
        let should_be_empty = " ------  ";
        let result = sanitize_dns_value(should_be_empty).expect_err("Expected error result");
        assert_eq!(
            should_be_empty,
            match result.kind() {
                ErrorKind::InvalidModuleName(x) => x,
                _ => panic!("Expected Result not Found"),
            }
        );
    }

    #[test]
    fn sanitize_dns_value_rfc1123_test_63chars() {
        let result = sanitize_dns_value_rfc1123(
            "01234567890123456789012345678901234567890123456789012345678901234567890123456789",
        )
        .unwrap();
        assert_eq!(
            result,
            "012345678901234567890123456789012345678901234567890123456789012"
        );
    }
    #[test]
    fn sanitize_dns_value_rfc1123_trim_and_lowercase() {
        let result = sanitize_dns_value_rfc1123("$edgeHub").unwrap();
        assert_eq!(result, "edgehub");
    }
    #[test]
    fn error_rfc1123_from_empty_string() {
        let should_be_empty = " ------  ";
        let result =
            sanitize_dns_value_rfc1123(should_be_empty).expect_err("Expected error result");
        assert_eq!(
            should_be_empty,
            match result.kind() {
                ErrorKind::InvalidModuleName(x) => x,
                _ => panic!("Expected Result not Found"),
            }
        );
    }

    #[test]
    fn label_values_are_sanitized() {
        let test_pairs: [[&str; 2]; 14] = [
            ["", "()"],
            ["e", "$e"],
            ["1", "1"],
            ["edgeagent", "$edgeAgent"],
            ["edgehub", "$edgeHub"],
            ["edgehub", "edge**Hub()"],
            ["12device", "12device"],
            ["345hub-name.org", "345hub-name.org"],
            // length is <= 63 characters, lowercase
            [
                "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabc",
                "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABC",
            ],
            // must be all alphanumeric characters or ['-','.','_']
            ["a-b_c.d", "a$?/-b#@_c=+.d"],
            // must start with an alphabet
            // must end with an alphanumeric character
            [
                "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijab----------c",
                "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB----------C",
            ],
            [
                "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijab",
                "ABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJABCDEFGHIJAB-------J",
            ],
            ["zz", "$-._/zz$-._/"],
            ["z9", "$-._/z9$-._/"],
        ];

        for pair in &test_pairs {
            assert_eq!(pair[0], sanitize_label_value(pair[1]))
        }
    }
}
