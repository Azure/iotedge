use std::io;

use actix_web::{web, App, HttpRequest, HttpResponse, HttpServer};
use serde_json;

mod modules;
mod state;

/* PR TODOS:
- parse method
- config parse for connection string, managmenet uri
- #cfg macro
- Path type for config file paths
- ask Raj about network interface comment - structopt  command line arguments (give ip address and port)
- 
*/

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

// returns an HttpResponse for creation of a new device
fn return_response(new_device: &state::Device) -> HttpResponse {
    // if the new_device is able to be created (fields are able to be parsed into JSON strings)
    match serde_json::to_string(new_device) {
        Ok(json_file) => HttpResponse::Ok().body(json_file),
        Err(_)        => HttpResponse::UnprocessableEntity().body("Unable to process device connection string. Are you sure the string is correctly set up?"),
    }
}

// returns a JSON representation of the current edge device's state
fn get_state(_req: HttpRequest) -> HttpResponse {
    if let Ok(contents) = state::get_file() { // if file exists and can be located
        let con_string = &state::get_connection_string(contents);
        let string_ref: Option<&str> = con_string.as_ref().map(|s| s.as_ref());
        if let Some(details) = string_ref { // if con_string is valid
            let mut new_device;
            if &details[1..10] == "HostName=" {
                new_device = state::Device::new(state::State::Manual, details.to_string());
            } else {
                new_device = state::Device::new(state::State::NotProvisioned, String::new());
            }
            return_response(&new_device)
        } else { // if con_string can't be converted to a valid device connection string
            HttpResponse::UnprocessableEntity().body("Device connection string unable to be converted")
        }
    } else { // if file doesn't exist or can't be located
        let new_device = state::Device::new(state::State::NotInstalled, String::new());
        return_response(&new_device)
    }
}

fn get_modules(_req: HttpRequest) -> HttpResponse {
    if let Ok(contents) = state::get_file() { // if file exists and can be located
        let mgmt_uri = modules::get_management_uri(&contents).unwrap();
        println!("LINE 56");
        let mod_list = modules::get_list(&mgmt_uri);
        println!("LINE 58");
        if let Ok(mods) = mod_list {
            println!("am i even getting here");
            HttpResponse::Ok().body("AHHHHHHHHHH")
            // HttpResponse::Ok().body(serde_json::to_string(&mods).unwrap())
        } else {
            HttpResponse::UnprocessableEntity().body("Unable to retrieve module list.")
        }
    } else { // if file doesn't exist or can't be located
        HttpResponse::UnprocessableEntity().body("Unable to find config.yaml file.")
    }
}

fn main() -> io::Result<()> {
    HttpServer::new(|| {
        App::new()
            .service(web::resource("/api/provisioning-state").route(web::get().to(get_state)))
            .service(web::resource("/api/modules").route(web::get().to(get_modules)))
    }).bind("127.0.0.1:8088")?.run()
}