use actix_web::{HttpServer, HttpRequest, Responder, HttpResponse, App, web};
use std::fs;
use std::io::{Result, Error};
use std::path::Path;
use std::env;

fn index(_req: &HttpRequest) -> impl Responder {
    let config_contents = get_file();
    match config_contents {
        Ok(connection_string) => {
            let con_string = get_connection_string(connection_string);
            let pre = "<ADD DEVICE CONNECTION STRING HERE>".to_string();
            match con_string {
                pre => populate_nm_json("Not provisioned"),
                contents => populate_json("manual", contents),
            }
        }
        Err(_) => populate_nm_json("Not installed")
    }
}

fn populate_json(state: &str, contents: String) -> String {
    let con_str = get_connection_string(contents);
    let (hub_name, device_id) = get_device_details(con_str);
    serde_json::json!({
        "state": state,
        "hub_name": hub_name,
        "device_id": device_id,
    }).to_string()
}

fn populate_nm_json(state: &str) -> String {
    serde_json::json!({"state": state}).to_string()
}

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
    dev_str[10..end].trim().to_string()
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
            println!("WINDOWS!");
            match env::var("CSIDL_COMMON_APPDATA") {
                Ok(val) => {
                    let path = format!("{}\\iotedge\\config.yaml", val);
                    get_config_file(&path)
                }
                Err(_) => match env::var("ProgramData") {
                    Ok(val) => {
                        let path = format!("{}\\iotedge\\config.yaml", val);
                        get_config_file(&path)
                    }
                    Err(_) => get_config_file("C:\\ProgramData\\iotedge\\config.yaml"),
                }
            }
        }
        _ => {
            println!("LINUX");
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

    let config_file = get_file();
    match config_file {
        Ok(contents) => {
            println!("My file contents: {}", contents);
        }
        Err(_) => {
            println!("Couldn't find file");
        }
    }
    
    // HttpServer::new(|| App::new().route("/api/provisioning-state", |r| r.f(index));
    
    // HttpServer::new(|| App::new().resource("/", |r| r.f(index)))
    //     .bind("127.0.0.1:8088")
    //     .run();
    
    let app = App::new().service(web::resource("/api/provisioning-state").to(index));

}

fn wtf(req: HttpRequest) -> HttpResponse {
    unimplemented!()
}