// Copyright (c) Microsoft. All rights reserved.

#[macro_use]
extern crate serde_derive;

mod error;
mod health;
mod settings;
mod state;

#[cfg(windows)]
use std::env;
use std::net::TcpStream;
use std::path::{Path, PathBuf};
use std::sync::Arc;

use actix_web::error::ErrorInternalServerError;
use actix_web::Error as ActixError;
use actix_web::*;
use edgelet_core::RuntimeSettings;
use edgelet_core::{Module as EdgeModule, ModuleRuntime, Provisioning};
use edgelet_docker::Settings as DockerSettings;
use edgelet_http_mgmt::*;
use futures::future::{ok, Either, IntoFuture};
use futures::Async;
use futures::Future;
use structopt::StructOpt;
use url::Url;

pub use error::Error;
use settings::Settings;

pub struct Context {
    pub edge_config: Result<DockerSettings, Error>,
    pub settings: Settings,
}

impl Context {
    pub fn new() -> Self {
        let settings = Settings::from_args();
        let edge_config = get_config(settings.config_path.as_ref().map(String::as_str));

        Context {
            edge_config,
            settings,
        }
    }
}

#[derive(Debug)]
pub struct Module {
    name: String,
    status: String,
}

impl Module {
    pub fn new(name: String, status: String) -> Self {
        Module { name, status }
    }

    pub fn name(&self) -> &String {
        &self.name
    }

    pub fn status(&self) -> &String {
        &self.status
    }
}

pub struct Main {
    context: Arc<Context>,
}

impl Main {
    pub fn new(context: Context) -> Self {
        Main {
            context: Arc::new(context),
        }
    }

    pub fn run(&self) -> Result<(), Error> {
        let address = format!(
            "{}:{}",
            self.context.settings.host, self.context.settings.port
        );

        println!("Server listening at http://{}", address);

        let context = web::Data::new(self.context.clone());
        let device = web::Data::new(set_up(context.clone()));

        HttpServer::new(move || {
            App::new()
                .register_data(context.clone())
                .register_data(device.clone())
                .service(
                    web::resource("/api/modules/{id}/restart").route(web::put().to(restart_module)),
                )
                .service(web::resource("/api/modules/").to_async(get_modules))
                .service(web::resource("/api/provisioning-state/").to(get_state))
                .service(web::resource("/api/connectivity/").to(get_connectivity))
                .service(web::resource("/api/health/").to_async(get_health))
        })
        .bind(address)?
        .run()?;

        Ok(())
    }
}

#[derive(Deserialize)]
pub struct AuthRequest {
    api_version: String,
}

fn set_up(context: web::Data<Arc<Context>>) -> Option<state::Device> {
    if let Ok(_) = state::get_file() {
        // if file exists and can be located
        context
            .get_ref()
            .edge_config
            .as_ref()
            .map(|config| {
                if let Provisioning::Manual(val) = config.provisioning() {
                    let dev_conn_str = val.device_connection_string();
                    if dev_conn_str == edgelet_core::DEFAULT_CONNECTION_STRING {
                        Some(state::Device::new(
                            state::State::NotProvisioned,
                            String::new(),
                        ))
                    } else {
                        Some(state::Device::new(
                            state::State::Manual,
                            dev_conn_str.to_string(),
                        ))
                    }
                } else {
                    // handle non-manual (dps/external) here
                    Some(state::Device::new(state::State::Manual, String::new()))
                }
            })
            .unwrap_or_else(|e| {
                println!("Err: {:?}", e);
                None
            })
    } else {
        // if file doesn't exist or can't be located
        Some(state::Device::new(
            state::State::NotInstalled,
            String::new(),
        ))
    }
}

fn get_state(
    _req: HttpRequest,
    device: web::Data<Option<state::Device>>,
    // _info: web::Query<AuthRequest>,
) -> HttpResponse {
    // println!("Query string version: {}", info.version);

    if let Some(dev) = device.get_ref() {
        println!("Dev: {:?}", dev);
        state::return_response(&dev)
    } else {
        HttpResponse::UnprocessableEntity().body("Device connection string unable to be processed.")
    }
}

fn get_modules(
    context: web::Data<Arc<Context>>,
    info: web::Query<AuthRequest>,
) -> Box<dyn Future<Item = HttpResponse, Error = ActixError>> {
    let api_ver = &info.api_version;
    return_modules(context, api_ver, module_response)
}

fn get_connectivity(_req: HttpRequest, device: web::Data<Option<state::Device>>) -> HttpResponse {
    if let Some(dev) = device.get_ref() {
        if let Some(iothub) = dev.hub_name() {
            let iothub_hostname = &format!("{}.azure-devices.net", iothub);

            let r = resolve_and_tls_handshake(&(&**iothub_hostname, 443), iothub_hostname);
            match r {
                Ok(_) => HttpResponse::Ok().body("Connected with IoT Hub!"),
                Err(_) => HttpResponse::UnprocessableEntity()
                    .body("Failed to establish connection with IoT Hub."),
            }
        } else {
            HttpResponse::UnprocessableEntity().body("IoT Hub name could not be processed")
        }
    } else {
        HttpResponse::UnprocessableEntity().body("IoT Hub name could not be processed")
    }
}

fn get_health(
    _req: HttpRequest,
    context: web::Data<Arc<Context>>,
    info: web::Query<AuthRequest>,
) -> Box<dyn Future<Item = HttpResponse, Error = ActixError>> {
    let api_ver = &info.api_version;
    return_modules(context, api_ver, health_response)
}

fn restart_module(
    req: HttpRequest,
    context: web::Data<Arc<Context>>,
    info: web::Query<AuthRequest>,
) -> HttpResponse {
    let api_ver = &info.api_version;

    let r = context
        .edge_config
        .as_ref()
        .map(|config| {
            let mgmt_uri = config.connect().management_uri();
            req.match_info().get("id").and_then(|module_id| {
                Url::parse(&format!("{}/modules/?api-version={}", mgmt_uri, api_ver))
                    .map_err(ErrorInternalServerError)
                    .and_then(|url| ModuleClient::new(&url).map_err(ErrorInternalServerError)) // can't connect to the endpoint
                    .ok()
                    .map(|mod_client| {
                        mod_client.restart(module_id);
                        HttpResponse::Ok()
                            .body(format!("Module {} was successfully restarted.", module_id))
                    })
            })
        })
        .unwrap_or_else(|_e| {
            Some(
                HttpResponse::ServiceUnavailable()
                    .body(format!("Unable to configure docker settings.")),
            )
        });

    if let Some(resp) = r {
        resp
    } else {
        HttpResponse::Ok().body("Failed to restart module.")
    }
}

fn return_modules(
    context: web::Data<Arc<Context>>,
    api_ver: &str,
    f: fn(Vec<Module>) -> HttpResponse,
) -> Box<dyn Future<Item = HttpResponse, Error = ActixError>> {
    let response = context
        .edge_config
        .as_ref()
        .map(move |config| {
            let mgmt_uri = config.connect().management_uri();
            // println!("API Version: {}", api_ver);
            Either::A(
                Url::parse(&format!("{}/modules/?api-version={}", mgmt_uri, api_ver))
                    .map_err(ErrorInternalServerError)
                    .and_then(|url| ModuleClient::new(&url).map_err(ErrorInternalServerError)) // can't connect to the endpoint
                    .map(move |mod_client| {
                        mod_client
                            .list()
                            .map(move |data| {
                                let x: Vec<Module> = data
                                    .iter()
                                    .map(move |c| {
                                        let mut status = "".to_string();
                                        if let Ok(Async::Ready(t)) = c.runtime_state().poll() {
                                            status =
                                                (*(t.status().clone()).to_string()).to_string();
                                        }
                                        Module::new(c.name().to_string(), status)
                                    })
                                    .collect();
                                f(x) // changes depending on method
                            })
                            .map_err(ErrorInternalServerError)
                    })
                    .into_future()
                    .flatten(),
            )
        })
        .unwrap_or_else(|err| {
            Either::B(ok(HttpResponse::ServiceUnavailable()
                .content_type("text/plain")
                .body(format!("{:?}", err))))
        });

    Box::new(response)
}

fn module_response(mods: Vec<Module>) -> HttpResponse {
    HttpResponse::Ok().body(format!("{:?}", mods))
}

fn health_response(mods: Vec<Module>) -> HttpResponse {
    let mut device_status = health::Status::new();
    let mut flag = true;
    for module in mods.iter() {
        if module.name() == "edgeAgent" {
            if module.status() == "success" {
                device_status.set_edge_agent();
            } else {
                flag = false;
            }
        } else if module.name() == "edgeHub" {
            if module.status() == "success" {
                device_status.set_edge_hub();
            } else {
                flag = false;
            }
        } else {
            if module.status() != "success" {
                flag = false;
            }
        }
    }
    device_status.set_other_modules(flag);
    let health = device_status.return_health();
    HttpResponse::Ok().body(format!(
        "Device health: {:?}\nDevice details: {:?}",
        health, device_status
    ))
}

fn resolve_and_tls_handshake(
    to_socket_addrs: &impl std::net::ToSocketAddrs,
    tls_hostname: &str,
) -> Result<(), ActixError> {
    let host_addr = to_socket_addrs
        .to_socket_addrs()
        .map_err(ErrorInternalServerError)?
        .next()
        .ok_or_else(|| "")
        .map_err(ErrorInternalServerError)?;

    let stream = TcpStream::connect_timeout(&host_addr, std::time::Duration::from_secs(10))
        .map_err(ErrorInternalServerError)?;

    let tls_connector = native_tls::TlsConnector::new().map_err(ErrorInternalServerError)?;

    let _ = tls_connector
        .connect(tls_hostname, stream)
        .map_err(ErrorInternalServerError)?;

    Ok(())
}

fn get_default_config_path() -> PathBuf {
    #[cfg(not(windows))]
    {
        Path::new("/etc/iotedge/config.yaml").to_owned()
        // Path::new("src/test.txt").to_owned()
    }

    #[cfg(windows)]
    {
        Path::new(
            env::var("CSIDL_COMMON_APPDATA")
                .or_else(|| env::var("ProgramData"))
                .unwrap_or("C:/ProgramData/iotedge/config.yaml"),
        )
        .to_owned()
    }
}

fn get_config(config_path: Option<&str>) -> Result<DockerSettings, Error> {
    let config_path = config_path
        .map(|p| Path::new(p).to_owned())
        .unwrap_or_else(get_default_config_path);
    Ok(DockerSettings::new(Some(&config_path))?)
}
