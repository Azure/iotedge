use actix_web::{HttpServer, HttpRequest, Responder, HttpResponse, App};
use std::fs;
use std::io::{Result};
use std::path::Path;

// fn index(_req: &HttpRequest) -> impl Responder {
//     let config_contents = get_file();
//     match config_contents {
//         Ok(connection_string) => {
//             let con_string = get_connection_string(connection_string);
//             match con_string {
//                 "<ADD DEVICE CONNECTION STRING HERE>".to_string() => populate_nm_json("Not provisioned"),
//                 contents => populate_json("manual", contents),
//             }
//         }
//         Err(_) => populate_nm_json("Not installed")
//     }
// }

// fn populate_json(state: &str, contents: String) -> serde_json::Value {
//     let (hub_name, device_ID) = get_connection_string(contents);
//     json!({
//         "state": state,
//         "hub_name": hub_name,
//         "device_ID": device_ID,
//     })
// }

// fn populate_nm_json(state: &str) -> serde_json::Value {
//     json!({"state": state})
// }

fn get_connection_string(contents: String) -> String {
    let pattern = "device_connection_string: ";
    let start = pattern_match(&contents, pattern) + pattern.len();
    let end = pattern_match(&contents, "# DPS TPM provisioning configuration");

    println!("Start index: {}", start);
    println!("End index: {}", end);
    contents[start..end].trim().to_string()
}

fn get_device_details(device_string: String) -> (String, String) {
    (get_hub_name(&device_string), get_device_id(&device_string))
}

fn get_hub_name(dev_str: &str) -> String {
    let end = pattern_match(dev_str, ".azure-devices.net");
    dev_str[9..end].trim().to_string()
}

fn get_device_id(dev_str: &str) -> String {
    let start = pattern_match(dev_str, "DeviceId=") + 9;
    let end = pattern_match(dev_str, ";SharedAccessKey=");
    dev_str[start..end].trim().to_string()
}

fn pattern_match(dev_str: &str, pattern: &str) -> usize {
    match dev_str.find(pattern) {
        Some(index) => index,
        None => 0, // Fugggggggggg
    }
}

fn get_file() -> Result<String> {
    match os_info::get().os_type() {
        os_info::Type::Windows => {
            get_config_file("C:\\ProgramData\\iotedge\\config.yaml")
        }
        _ => {
            get_config_file("/etc/iotedge/config.yaml")
        }
    }
}

fn get_config_file(path: &str) -> Result<String> {
    fs::read_to_string(path)
}

fn main() {
    let val = match get_config_file(".\\src\\test.txt") {
        Err(_) => {
            println!("HECKC ");
            "".to_string()
        }
        Ok(heck) => {
            println!("SUCCESS!");
            heck
        }
    };

    println!("Connection String: {}", get_connection_string(val));

    // HttpServer::new(|| App::new().route("/", HttpResponse::Ok()))
    //     .bind("127.0.0.1:8888")
    //     .unwrap()
    //     .start();
}
