use actix_web::{HttpServer, HttpRequest, HttpResponse, App, web};
use std::fmt::{self, Display, Formatter};
use std::fs;
use std::io::{self, Result};
use std::env;
use serde::{Deserialize, Serialize};

/* SOME ISSUES:
    - Linux path to get config.yaml hasn't been tested
    - DPS state is TBD on how to handle
    - CSIDL env var hasn't been tried/tested
    - HttpResponse types probably incorrect
    - Running after compiling for the first time - when accessing localhost, I get PageNotFound which auto-refreshes to the expected JSON display after a couple of seconds
*/

/* Design Questions:
    - Parsing has hard-coded values to search for based on the one config.yaml file I've looked at (i.e. "DeviceId=") - is that okay?
    - Functions all return/pass up an Option<> - is that good practice?
    - Nested 'match' control structures are everywhere yikes :(
    - To make a Device, I pass in a string state (i.e. "manual"), convert it to a State enum, then implement Display to show "Manual" - :\ that just seems wrong
    - Function main() returns a Result<()> - is that appropriate?
    - Functions are in no particular order seems kinda messy and hard to navigate :\
*/

#[derive(Serialize, Deserialize)]
enum State {
    Manual,
    NotProvisioned,
    NotInstalled,
    // DPS
}

impl Display for State {
    fn fmt(&self, f: &mut Formatter) -> fmt::Result {
        match self {
            State::Manual => write!(f, "Manual"),
            State::NotProvisioned => write!(f, "Not Provisioned"),
            State::NotInstalled => write!(f, "Not Installed"),
            // State::DPS => write!(f, "DPS"),
        }
    }
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
            "manual" => {
                Device::manual_rep(con_string)
            }
            "not provisioned" => {
                Device {
                    state: State::NotProvisioned,
                    hub_name: None,
                    device_id: None,
                }
            }
            _ => {
                Device {
                    state: State::NotInstalled,
                    hub_name: None,
                    device_id: None,
                }
            }
            // "dps" =>
        }
    }

    // returns the manual state representation of device
    fn manual_rep(con_string: String) -> Device {
        match get_device_details(con_string) {
            Some((hub_name, device_id)) => {
                Device {
                    state: State::Manual,
                    hub_name: Some(hub_name),
                    device_id: Some(device_id),
                }
            }
            None => {
                Device {
                    state: State::Manual,
                    hub_name: None,
                    device_id: None,
                }
            }
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

// returns the hub name of the edge device
fn get_hub_name(dev_str: &str) -> Option<String> {
    let start_pattern = "HostName=";
    let start = dev_str.find(start_pattern)? + start_pattern.len();
    let end = dev_str.find(".azure-devices.net")?;
    Some(dev_str[start..end].trim().to_string())
}

// returns the device ID of the edge device
fn get_device_id(dev_str: &str) -> Option<String> {
    let start_pattern = "DeviceId=";
    let start = dev_str.find(start_pattern)? + start_pattern.len();
    let end = dev_str.find(";SharedAccessKey=")?;
    Some(dev_str[start..end].trim().to_string())
}

// returns the contents of the config.yaml file from the current device
fn get_file() -> Result<String> {
    match os_info::get().os_type() {
        os_info::Type::Windows => {
            match env::var("CSIDL_COMMON_APPDATA") {
                /* *** 'Ok' branch hasn't been tested to see if it works *** */
                Ok(val) => { 
                    println!("Using CSIDL_COMMON_APPDATA");
                    fs::read_to_string(&format!("{}\\iotedge\\config.yaml", val))
                }
                Err(_) => match env::var("ProgramData") {
                    Ok(val) => {
                        println!("Using ProgramData");
                        fs::read_to_string(&format!("{}\\iotedge\\config.yaml", val))
                    }
                    Err(_) => { 
                        println!("Using default");
                        fs::read_to_string("C:\\ProgramData\\iotedge\\config.yaml")
                    }
                }
            }
            // fs::read_to_string(".\\src\\tests.txt")
        }
        /* *** '_' branch hasn't been tested yet*** */
        _ => fs::read_to_string("/etc/iotedge/config.yaml"),  
    }
}

// returns a JSON representation of the current edge device's state
fn get_state(_req: HttpRequest) -> HttpResponse {
    match get_file() {
        // if the file exists and can be located
        Ok(contents) => {
            let con_string = &get_connection_string(contents);
            let string_ref: Option<&str> = con_string.as_ref().map(|s| s.as_ref());
            match string_ref {
                // if con_string contained a valid string
                Some(details) => { 
                    let mut new_device;
                    if &details[1..10] == "HostName=" {
                        new_device = Device::new("manual", details.to_string());
                    } else {
                        new_device = Device::new("not provisioned", String::new());
                    }
                    return_response(&new_device)
                }
                // if con_string couldn't be converted to a device connection string
                None => HttpResponse::UnprocessableEntity().body("Device connection string unable to be converted")
            }
        }
        // if the file doesn't exist or can't be located
        Err(_) => {
            let new_device = Device::new("not installed", String::new());
            return_response(&new_device)
        }
    }
}

// returns an HttpResponse for creation of a new device
fn return_response(new_device: &Device) -> HttpResponse {
    // if the new_device is able to be created (fields are able to be parsed into JSON strings)
    match serde_json::to_string(new_device) {
        Ok(json_file) => HttpResponse::Ok().body(json_file),
        Err(_) => HttpResponse::UnprocessableEntity().body("Unable to process device connection string. Are you sure the string is correct?"),
    }
}

fn main() -> io::Result<()> {
    HttpServer::new(|| App::new()
        .service(web::resource("/api/provisioning-state")
        .route(web::get().to(get_state))))
        .bind("127.0.0.1:8088")?
        .run()
}