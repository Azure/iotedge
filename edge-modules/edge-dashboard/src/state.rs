use std::fs;
use std::io::Result;
use std::env;
use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug)]
pub enum State {
    Manual,
    NotProvisioned,
    NotInstalled,
    // DPS
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
    pub fn new(state: &str, con_string: String) -> Device {
        match state {
            "manual"          => Device::manual_rep(con_string),
            "not provisioned" => Device::default(State::NotProvisioned),
            // "dps"          => ,
            _                 => Device::default(State::NotInstalled),
        }
    }

    // returns the manual state representation of device
    pub fn manual_rep(con_string: String) -> Device {
        if let Some((hub_name, device_id)) = get_device_details(con_string) {
            Device {
                state: State::Manual,
                hub_name: Some(hub_name),
                device_id: Some(device_id),
            }
        } else {
            Device::default(State::Manual)
        }
    }

    // returns a Device of specified state with all other fields as None
    pub fn default(state: State) -> Device {
        Device {
            state: state,
            hub_name: None,
            device_id: None,
        }
    }
}

// returns the connection string given the config.yaml file contents
pub fn get_connection_string(contents: String) -> Option<String> {
    let pattern = "device_connection_string: ";
    let start = &contents.find(pattern)? + pattern.len();
    let end = &contents.find("# DPS TPM provisioning configuration")? + 0;
    let con_string = contents[start..end].trim().to_string();
    println!("con_string: {}", con_string);
    Some(con_string)
}

// returns a tuple of the hub name and device IDs
pub fn get_device_details(device_string: String) -> Option<(String, String)> {
    let hub_name = get_hub_name(&device_string)?;
    let device_id = get_device_id(&device_string)?;
    println!("hub_name: {}", hub_name);
    println!("device_id: {}", device_id);
    Some((hub_name, device_id))
}

// TODO - replace with a better parser (from edgelet)
// returns the hub name of the edge device
pub fn get_hub_name(dev_str: &str) -> Option<String> {
    let start_pattern = "HostName=";
    let start = dev_str.find(start_pattern)? + start_pattern.len();
    let end = dev_str.find(".azure-devices.net")?;
    Some(dev_str[start..end].trim().to_string())
}

// TODO - replace with a better parser (from edgelet)
// returns the device ID of the edge device
pub fn get_device_id(dev_str: &str) -> Option<String> {
    let start_pattern = "DeviceId=";
    let start = dev_str.find(start_pattern)? + start_pattern.len();
    let end = dev_str.find(";SharedAccessKey=")?;
    Some(dev_str[start..end].trim().to_string())
}

// returns the contents of the config.yaml file from the current device
pub fn get_file() -> Result<String> {
    if os_info::get().os_type() == os_info::Type::Windows {
        if let Ok(csidl_path) = env::var("CSIDL_COMMON_APPDATA") { // in my experience, this never gets here
            fs::read_to_string(&format!("{}\\iotedge\\config.yaml", csidl_path))
        } else if let Ok(program_path) = env::var("ProgramData") {
            fs::read_to_string(&format!("{}\\iotedge\\config.yaml", program_path))
        } else {
            fs::read_to_string("C:\\ProgramData\\iotedge\\config.yaml")
        }
        // fs::read_to_string(".\\src\\tests.txt")
    } else { // this branch hasn't been tested yet
        fs::read_to_string("/etc/iotedge/config.yaml")
    }
}