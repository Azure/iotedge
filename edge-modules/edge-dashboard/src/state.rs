extern crate yaml_rust;

use std::collections::HashMap;
use std::env;
use std::fs;
use std::io::Result;
use std::path::Path;

use serde::{Deserialize, Serialize};
use yaml_rust::YamlLoader;

#[derive(Serialize, Deserialize, Debug)]
pub enum State {
    Manual,
    NotProvisioned,
    NotInstalled,
    // TODO: DPS
}

#[derive(Serialize, Deserialize)]
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
        // println!("{:?}", map);
        Device {
            state: state,
            hub_name: get_val(&map, "HostName"),
            device_id: get_val(&map, "DeviceId"),
        }
    }
}
// returns the connection string given the config.yaml file contents
pub fn get_connection_string(contents: String) -> Option<String> {
    if let Ok(doc) = YamlLoader::load_from_str(&contents) {
        doc[0]["provisioning"]["device_connection_string"]
            .as_str()
            .map(|c| c.to_string())
    } else {
        None
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

// returns the config.yaml file if the OS at runtime is windows
pub fn handle_windows() -> Result<String> {
    if let Ok(csidl_path) = env::var("CSIDL_COMMON_APPDATA") {
        return fs::read_to_string(Path::new(&csidl_path).join("/iotedge/config.yaml"));
    } else if let Ok(program_path) = env::var("ProgramData") {
        return fs::read_to_string(Path::new(&program_path).join("/iotedge/config.yaml"));
    }
    fs::read_to_string(Path::new("C:/ProgramData/iotedge/config.yaml"))
    // fs::read_to_string(".\\src\\tests.txt")
}

// returns the contents of the config.yaml file from the current device
pub fn get_file() -> Result<String> {
    #[cfg(target_os = "windows")]
    return handle_windows();

    #[cfg(target_os = "linux")]
    fs::read_to_string("src/test.txt")
    // fs::read_to_string("/etc/iotedge/config.yaml")
}
