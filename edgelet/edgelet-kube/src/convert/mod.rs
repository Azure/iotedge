// Copyright (c) Microsoft. All rights reserved.

#[cfg(test)]
use crate::error::{ErrorKind, Result};
#[cfg(test)]
use edgelet_utils::sanitize_dns_label;

mod to_docker;
mod to_k8s;

pub use self::to_docker::pod_to_module;
pub use self::to_k8s::spec_to_deployment;

#[cfg(test)]
pub fn sanitize_dns_value(name: &str) -> Result<String> {
    let name_string = sanitize_dns_label(name);
    if name_string.is_empty() {
        Err(ErrorKind::InvalidModuleName(name.to_owned()))?
    } else {
        Ok(name_string)
    }
}

#[cfg(test)]
mod tests {

    use super::*;

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
}
