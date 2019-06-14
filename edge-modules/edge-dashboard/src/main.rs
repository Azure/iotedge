use actix_web::{HttpServer, HttpRequest, HttpResponse, App, web};
use std::fmt::{self, Display, Formatter};
use std::fs;
use std::io::Result;
use std::env;
use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize)]
enum State {
    Manual,
    Not_Provisioned,
    Not_Installed,
    // DPS
}

impl Display for State {
    fn fmt(&self, f: &mut Formatter) -> fmt::Result {
        match self {
            State::Manual => write!(f, "Manual"),
            State::Not_Provisioned => write!(f, "Not Provisioned"),
            State::Not_Installed => write!(f, "Not Installed"),
            // State::DPS => write!(f, "DPS"),
        }
    }
}

#[derive(Serialize, Deserialize)]
struct Device {
    state: State,
    hub_name: Option<String>,
    device_id: Option<String>,
}

impl Device {
    // makes a new Device struct based on the state
    fn new(state: &str, con_string: String) -> Device {
        match state {
            "manual" => {
                let (hub_name, device_id) = get_device_details(con_string).unwrap();
                Device {
                    state: State::Manual,
                    hub_name: Some(hub_name),
                    device_id: Some(device_id),
                }
            }
            "not provisioned" => {
                Device {
                    state: State::Not_Provisioned,
                    hub_name: None,
                    device_id: None,
                }
            }
            _ => {
                Device {
                    state: State::Not_Installed,
                    hub_name: None,
                    device_id: None,
                }
            }
            // "dps" =>
        }
    }
}

// returns the connection string given the config.yaml file contents
fn get_connection_string(contents: String) -> Option<String> {
    let pattern = "device_connection_string: ";
    let start = &contents.find(pattern)? + pattern.len();
    let end = &contents.find("# DPS TPM provisioning configuration")? + 0;

    println!("Start index: {}", start);
    println!("End index: {}", end);
    Some(contents[start..end].trim().to_string())
}

// returns a tuple of the hub name and device IDs
fn get_device_details(device_string: String) -> Option<(String, String)> {
    let hub_name = get_hub_name(&device_string)?;
    let device_id = get_device_id(&device_string)?;
    Some((hub_name, device_id))
}

// returns the hub name of the edge device
fn get_hub_name(dev_str: &str) -> Option<String> {
    let end = dev_str.find(".azure-devices.net")?;
    Some(dev_str[10..end].trim().to_string())
}

// returns the device ID of the edge device
fn get_device_id(dev_str: &str) -> Option<String> {
    let start = dev_str.find("DeviceId=")? + 9;
    let end = dev_str.find(";SharedAccessKey=")?;
    Some(dev_str[start..end].trim().to_string())
}

// returns the contents of the config.yaml file from the current device
fn get_file() -> Result<String> {
    match os_info::get().os_type() {
        os_info::Type::Windows => {
            match env::var("CSIDL_COMMON_APPDATA") {
                Ok(val) => fs::read_to_string(&format!("{}\\iotedge\\config.yaml", val)),
                Err(_) => match env::var("ProgramData") {
                    Ok(val) => fs::read_to_string(&format!("{}\\iotedge\\config.yaml", val)),
                    Err(_) => fs::read_to_string("C:\\ProgramData\\iotedge\\config.yaml"),
                }
            }
        }
        _ => fs::read_to_string("/etc/iotedge/config.yaml"),
    }
}

// returns a JSON representation of the current edge device's state
fn get_state(_req: HttpRequest) -> HttpResponse {
    match get_file() {
        Ok(contents) => {
            let con_string = &get_connection_string(contents);
            // match con_string {
            //     Some(val) => println!("Connection String: {}", val),
            //     None => ()
            // }
            // println!("Connection String: {}", con_string);
            let string_ref: Option<&str> = con_string.as_ref().map(|s| s.as_ref());
            match string_ref {
                Some("<ADD DEVICE CONNECTION STRING HERE>") => {
                    let new_device = Device::new("not provisioned", String::new());
                    HttpResponse::Ok().body(serde_json::to_string(&new_device).unwrap())
                }
                Some(details) => {
                    let new_device = Device::new("manual", details.to_string());
                    HttpResponse::Ok().body(serde_json::to_string(&new_device).unwrap())
                }
                None => HttpResponse::NotFound().body("File or device connection string not found")
            }
        }
        Err(_) => {
            let new_device = Device::new("not installed", String::new());
            HttpResponse::Ok().body(serde_json::to_string(&new_device).unwrap())
        }
    }
}

fn main() {
    HttpServer::new(|| App::new()
        .service(web::resource("/api/provisioning-state")
        .route(web::get().to(get_state))))
        .bind("127.0.0.1:8088")
        .unwrap().run();
}