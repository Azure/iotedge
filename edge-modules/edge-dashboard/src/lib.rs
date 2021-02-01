// Copyright (c) Microsoft. All rights reserved.

mod error;
mod health;
mod modules;
mod settings;
mod state;
mod status;

#[cfg(windows)]
use std::env;
use std::path::{Path, PathBuf};
use std::sync::Arc;

use actix_cors::Cors;
use actix_web::*;
use edgelet_core::Provisioning;
use edgelet_core::RuntimeSettings;
use edgelet_docker::Settings as DockerSettings;
use serde_derive::Deserialize;
use structopt::StructOpt;

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
                .wrap(Cors::new().send_wildcard())
                .register_data(context.clone())
                .register_data(device.clone())
                .service(
                    web::resource("/api/modules/{id}/restart").to_async(modules::restart_module),
                )
                .service(web::resource("/api/modules/{id}/logs").to_async(modules::get_logs))
                .service(web::resource("/api/modules").to_async(modules::get_modules))
                .service(web::resource("/api/health").to_async(modules::get_health))
                .service(web::resource("/api/provisioning-state").to(status::get_state))
                .service(web::resource("/api/connectivity").to(status::get_connectivity))
                .service(web::resource("/api/diagnostics").to(status::get_diagnostics))
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
                if let ProvisioningType::Manual(val) = config.provisioning() {
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

fn get_default_config_path() -> PathBuf {
    {
        Path::new("/etc/aziot/edged/config.yaml").to_owned()
    }
}

fn get_config(config_path: Option<&str>) -> Result<DockerSettings, Error> {
    let config_path = config_path
        .map(|p| Path::new(p).to_owned())
        .unwrap_or_else(get_default_config_path);
    Ok(DockerSettings::new(Some(&config_path))?)
}
