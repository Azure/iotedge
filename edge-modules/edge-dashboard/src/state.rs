// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::fs;
use std::io::Result;

use actix_web::*;
use serde::{Deserialize, Serialize};

#[derive(Clone, Serialize, Deserialize, Debug)]
pub enum State {
    Manual,
    NotProvisioned,
    NotInstalled,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct Device {
    state: State,
    #[serde(skip_serializing_if = "Option::is_none")]
    hub_name: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    device_id: Option<String>,
}

impl Device {
    // makes a new Device struct based on the state
    pub fn new(state: State, conn_str: String) -> Self {
        let map = parse_query(&conn_str, ';', '=');
        Device {
            state: state,
            hub_name: get_val(&map, "HostName"),
            device_id: get_val(&map, "DeviceId"),
        }
    }

    pub fn _state(&self) -> &State {
        &self.state
    }

    pub fn hub_name(&self) -> &Option<String> {
        &self.hub_name
    }

    pub fn _device_id(&self) -> &Option<String> {
        &self.device_id
    }
}

// returns the value as an Option from the map
pub fn get_val(map: &HashMap<&str, String>, key: &str) -> Option<String> {
    if key == "HostName" {
        if let Some(val) = map.get("HostName") {
            let list: Vec<&str> = val.split('.').collect();
            return Some(list[0].to_string());
        } else {
            return None;
        }
    }
    map.get(key).map(|val| val.to_string())
}

// parses the given connection string by delimiters
pub fn parse_query(query: &str, pair_delim: char, kv_delim: char) -> HashMap<&str, String> {
    query
        .split(pair_delim)
        .filter_map(|seg| {
            if seg.is_empty() {
                None
            } else {
                let mut tokens = seg.splitn(2, kv_delim);
                if let Some(key) = tokens.next() {
                    let val = tokens.next().unwrap_or("").to_string();
                    Some((key, val))
                } else {
                    None
                }
            }
        })
        .collect()
}

// returns the contents of the config.yaml file from the current device
pub fn get_file() -> Result<String> {
    fs::read_to_string("/etc/aziot/edged/config.yaml")
}

pub fn return_response(new_device: &Device) -> HttpResponse {
    // if the new_device is able to be created (fields are able to be parsed into JSON strings)
    match serde_json::to_string(new_device) {
        Ok(json_file) => HttpResponse::Ok().body(json_file),
        Err(_)        => HttpResponse::UnprocessableEntity().body("Unable to process device connection string. Are you sure the string is correctly set up?"),
    }
}
