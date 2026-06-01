// Copyright (c) Microsoft. All rights reserved.

mod error;
mod yaml_file_source;

use std::collections::HashMap;

pub use crate::error::Error;
pub use crate::yaml_file_source::YamlFileSource;

#[inline]
pub fn ensure_not_empty(value: &str) -> Result<(), Error> {
    if value.trim().is_empty() {
        return Err(Error::ArgumentEmpty);
    }

    Ok(())
}

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
    format!("URI: azureiot://{hub_name}/devices/{device_id}/modules/{module_id}")
}
