// Copyright (c) Microsoft. All rights reserved.

#[macro_use]
extern crate serde_derive;

mod error;
mod modules;
mod settings;
mod state;

#[cfg(windows)]
use std::env;
use std::path::{Path, PathBuf};
use std::sync::Arc;

use actix_web::error::ErrorInternalServerError;
use actix_web::Error as ActixError;
use actix_web::*;
use edgelet_config::Provisioning;
use edgelet_config::Settings as EdgeSettings;
use edgelet_core::{Module, ModuleRuntime};
use edgelet_docker::DockerConfig;
use edgelet_http_mgmt::*;
use futures::future::{ok, Either, IntoFuture};
use futures::Async;
use futures::Future;
use structopt::StructOpt;
use url::Url;

pub use error::Error;
use settings::Settings;

pub struct Context {
    pub edge_config: Result<EdgeSettings<DockerConfig>, Error>,
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
pub struct SparseModule {
    name: String,
    // id: String,
    status: String,
}

impl SparseModule {
    pub fn new(name: String, status: String) -> Self {
        SparseModule {
            name,
            // id,
            status,
        }
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
        HttpServer::new(move || {
            App::new()
                .register_data(context.clone())
                .service(web::resource("/api/modules/").to_async(get_modules))
                .service(web::resource("/api/provisioning-state/").to(get_state))
        })
        .bind(address)?
        .run()?;

        Ok(())
    }
}

#[derive(Deserialize)]
pub struct AuthRequest {
    version: String,
}

fn get_state(
    _req: HttpRequest,
    context: web::Data<Arc<Context>>,
    _info: web::Query<AuthRequest>,
) -> HttpResponse {
    // println!("Query string version: {}", info.version);
    if let Ok(_) = state::get_file() {
        // if file exists and can be located
        context
            .edge_config
            .as_ref()
            .map(|config| {
                let mut new_device;
                if let Provisioning::Manual(val) = config.provisioning() {
                    let dev_conn_str = val.device_connection_string();
                    if dev_conn_str == edgelet_config::DEFAULT_CONNECTION_STRING {
                        new_device = state::Device::new(state::State::NotProvisioned, String::new())
                    } else {
                        new_device =
                            state::Device::new(state::State::Manual, dev_conn_str.to_string());
                    }
                } else {
                    // handle non-manual (dps/external) here
                    new_device = state::Device::new(state::State::Manual, String::new());
                }
                state::return_response(&new_device)
            })
            .unwrap_or_else(|_| {
                HttpResponse::UnprocessableEntity()
                    .body("Device connection string unable to be processed")
            })
    } else {
        // if file doesn't exist or can't be located
        let new_device = state::Device::new(state::State::NotInstalled, String::new());
        state::return_response(&new_device)
    }
}

pub fn get_modules(
    context: web::Data<Arc<Context>>,
    info: web::Query<AuthRequest>,
) -> Box<dyn Future<Item = HttpResponse, Error = ActixError>> {
    let response = context
        .edge_config
        .as_ref()
        .map(|config| {
            let mgmt_uri = config.connect().management_uri();
            let api_ver = &info.version;
            // println!("API Version: {}", api_ver);
            Either::A(
                Url::parse(&format!("{}/modules/?api-version={}", mgmt_uri, api_ver))
                    .map_err(ErrorInternalServerError)
                    .and_then(|url| ModuleClient::new(&url).map_err(ErrorInternalServerError))
                    .map(|mod_client| {
                        mod_client
                            .list()
                            .map(|data| {
                                let x: Vec<SparseModule> = data
                                    .iter()
                                    .map(|c| {
                                        let mut status = "".to_string();
                                        if let Ok(Async::Ready(t)) = c.runtime_state().poll() {
                                            status =
                                                (*(t.status().clone()).to_string()).to_string();
                                        }
                                        SparseModule::new(c.name().to_string(), status)
                                    })
                                    .collect();
                                HttpResponse::Ok()
                                    .content_type("text/html")
                                    .body(format!("{:?}", x))
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

fn get_config(config_path: Option<&str>) -> Result<EdgeSettings<DockerConfig>, Error> {
    let config_path = config_path
        .map(|p| Path::new(p).to_owned())
        .unwrap_or_else(get_default_config_path);
    Ok(EdgeSettings::<DockerConfig>::new(Some(&config_path))?)
}
