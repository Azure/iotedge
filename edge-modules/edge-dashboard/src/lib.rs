// Copyright (c) Microsoft. All rights reserved.

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
use serde_derive::Deserialize;
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
                .service(web::resource("/api/modules/{id}/restart").to_async(restart_module))
                .service(web::resource("/api/modules").to_async(get_modules))
                .service(web::resource("/api/provisioning-state").to(get_state))
                .service(web::resource("/api/connectivity").to(get_connectivity))
                .service(web::resource("/api/health").to_async(get_health))
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
                println!("Error: {:?}", e);
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

fn get_state(device: web::Data<Option<state::Device>>) -> HttpResponse {
    if let Some(dev) = device.get_ref() {
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
                Ok(_) => HttpResponse::Ok().body(""),
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
) -> Box<dyn Future<Item = HttpResponse, Error = ActixError>> {
    let module_id = req.match_info().get("id");
    let api_ver = &info.api_version;
    let response = context
        .edge_config
        .as_ref()
        .map(|config| {
            let mgmt_uri = config.connect().management_uri();
            Either::A(
                Url::parse(&format!("{}/modules/?api-version={}", mgmt_uri, api_ver))
                    .map_err(ErrorInternalServerError)
                    .and_then(|url| ModuleClient::new(&url).map_err(ErrorInternalServerError))
                    .map(|mod_client| {
                        if let Some(id) = module_id {
                            mod_client.restart(id)
                        } else {
                            mod_client.restart("")
                        }
                        .map(|_| HttpResponse::Ok().body(""))
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
            Either::A(
                Url::parse(&format!("{}/modules/?api-version={}", mgmt_uri, api_ver))
                    .map_err(ErrorInternalServerError)
                    .and_then(|url| ModuleClient::new(&url).map_err(ErrorInternalServerError))
                    .map(|mod_client| {
                        mod_client
                            .list()
                            .map(move |data| {
                                let mods: Vec<Module> = data
                                    .iter()
                                    .map(move |c| {
                                        let status =
                                            if let Ok(Async::Ready(t)) = c.runtime_state().poll() {
                                                (*(t.status().clone()).to_string()).to_string()
                                            } else {
                                                "".to_string()
                                            };
                                        Module::new(c.name().to_string(), status)
                                    })
                                    .collect();
                                f(mods) // changes depending on API call
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
    let mut edge_agent = false;
    let mut edge_hub = false;
    let mut other = true;

    for module in mods.iter() {
        if module.name() == "edgeAgent" {
            if module.status() == "running" {
                edge_agent = true;
            }
        } else if module.name() == "edgeHub" {
            if module.status() == "running" {
                edge_hub = true;
            }
        } else {
            if module.status() != "running" {
                other = false;
            }
        }
    }

    device_status.set_iotedged();
    device_status.set_edge_agent(edge_agent);
    device_status.set_edge_hub(edge_hub);
    device_status.set_other_modules(edge_agent && edge_hub && other);

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
