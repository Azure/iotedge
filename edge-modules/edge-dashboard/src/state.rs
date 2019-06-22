use std::env;
use std::io::Result;
use std::fs;
use std::path::Path;
use serde::{Deserialize, Serialize};

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
        Device {
            state: state,
            hub_name: get_hub_name(&conn_str),
            device_id: get_device_id(&conn_str),
        }
    }
}

// returns the connection string given the config.yaml file contents
pub fn get_connection_string(contents: String) -> Option<String> {
    let pattern = "device_connection_string: ";
    let start = &contents.find(pattern)? + pattern.len();
    let end = &contents.find("# DPS TPM provisioning configuration")? + 0;
    let conn_str = contents[start..end].trim().to_string();
    println!("conn_str: {}", conn_str);
    Some(conn_str)
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
        if let Ok(csidl_path) = env::var("CSIDL_COMMON_APPDATA") {
            // use join method
            fs::read_to_string(Path::new(&format!("{}/iotedge/config.yaml", csidl_path)))
        } else if let Ok(program_path) = env::var("ProgramData") {
            fs::read_to_string(Path::new(&format!("{}/iotedge/config.yaml", program_path)))
        } else {
            fs::read_to_string(Path::new("C:/ProgramData/iotedge/config.yaml"))
        }
        // fs::read_to_string(".\\src\\tests.txt")
    } else { // this branch hasn't been tested yet
        fs::read_to_string("/etc/iotedge/config.yaml")
    }
}