use actix_web::{HttpServer, HttpRequest, HttpResponse, App, web};
use std::fs;
use std::io::{self, Result};
use std::env;
use serde::{Deserialize, Serialize};

/* SOME ISSUES:
    - Linux path to get config.yaml hasn't been tested
    - DPS state is TBD on how to handle
    - CSIDL env var hasn't been tried/tested
    - HttpResponse types probably incorrect
    - Running after compiling for the first time - when accessing localhost, I get "refused to connect" which auto-refreshes to the expected JSON display after a couple of seconds
*/

/* TODOs:
    - Check edgelet code to see better parser (split on ';', etc.)
    - Change functions to return Result<_, Error> + make custom Error modules crate
        ** can import Cargo.toml dependencies from other folders
*/

#[derive(Serialize, Deserialize, Debug)]
enum State {
    Manual,
    NotProvisioned,
    NotInstalled,
    // DPS
}

#[derive(Serialize, Deserialize)]
struct Device {
    state: State,
    #[serde(skip_serializing_if = "Option::is_none")]
    hub_name: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    device_id: Option<String>,
}

impl Device {
    // makes a new Device struct based on the state
    fn new(state: &str, con_string: String) -> Device {
        match state {
            "manual"          => Device::manual_rep(con_string),
            "not provisioned" => Device::default(State::NotProvisioned),
            // "dps"          => ,
            _                 => Device::default(State::NotInstalled),
        }
    }

    // returns the manual state representation of device
    fn manual_rep(con_string: String) -> Device {
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
    fn default(state: State) -> Device {
        Device {
            state: state,
            hub_name: None,
            device_id: None,
        }
    }
}

// returns the connection string given the config.yaml file contents
fn get_connection_string(contents: String) -> Option<String> {
    let pattern = "device_connection_string: ";
    let start = &contents.find(pattern)? + pattern.len();
    let end = &contents.find("# DPS TPM provisioning configuration")? + 0;
    let con_string = contents[start..end].trim().to_string();
    println!("con_string: {}", con_string);
    Some(con_string)
}

// returns a tuple of the hub name and device IDs
fn get_device_details(device_string: String) -> Option<(String, String)> {
    let hub_name = get_hub_name(&device_string)?;
    let device_id = get_device_id(&device_string)?;
    println!("hub_name: {}", hub_name);
    println!("device_id: {}", device_id);
    Some((hub_name, device_id))
}

// TODO - replace with a better parser (from edgelet)
// returns the hub name of the edge device
fn get_hub_name(dev_str: &str) -> Option<String> {
    let start_pattern = "HostName=";
    let start = dev_str.find(start_pattern)? + start_pattern.len();
    let end = dev_str.find(".azure-devices.net")?;
    Some(dev_str[start..end].trim().to_string())
}

// TODO - replace with a better parser (from edgelet)
// returns the device ID of the edge device
fn get_device_id(dev_str: &str) -> Option<String> {
    let start_pattern = "DeviceId=";
    let start = dev_str.find(start_pattern)? + start_pattern.len();
    let end = dev_str.find(";SharedAccessKey=")?;
    Some(dev_str[start..end].trim().to_string())
}

// returns the contents of the config.yaml file from the current device
fn get_file() -> Result<String> {
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

// returns a JSON representation of the current edge device's state
fn get_state(_req: HttpRequest) -> HttpResponse {
    if let Ok(contents) = get_file() { // if file exists and can be located
        let con_string = &get_connection_string(contents);
        let string_ref: Option<&str> = con_string.as_ref().map(|s| s.as_ref());
        if let Some(details) = string_ref { // if con_string is valid
            let mut new_device;
            if &details[1..10] == "HostName=" {
                new_device = Device::new("manual", details.to_string());
            } else {
                new_device = Device::new("not provisioned", String::new());
            }
            return_response(&new_device)
        } else { // if con_string can't be converted to a valid device connection string
            HttpResponse::UnprocessableEntity().body("Device connection string unable to be converted")
        }
    } else { // if file doesn't exist or can't be located
        let new_device = Device::new("not installed", String::new());
        return_response(&new_device)
    }
}

// returns an HttpResponse for creation of a new device
fn return_response(new_device: &Device) -> HttpResponse {
    // if the new_device is able to be created (fields are able to be parsed into JSON strings)
    match serde_json::to_string(new_device) {
        Ok(json_file) => HttpResponse::Ok().body(json_file),
        Err(_)        => HttpResponse::UnprocessableEntity().body("Unable to process device connection string. Are you sure the string is correctly set up?"),
    }
}

fn main() -> io::Result<()> {
    HttpServer::new(|| App::new()
        .service(web::resource("/api/provisioning-state")
        .route(web::get().to(get_state))))
        .bind("127.0.0.1:8088")?
        .run()
}