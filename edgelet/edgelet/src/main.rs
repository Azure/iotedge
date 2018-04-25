// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate clap;
extern crate config;
extern crate edgelet_core;
extern crate edgelet_docker;
extern crate edgelet_http;
extern crate edgelet_http_mgmt;
extern crate edgelet_http_workload;
extern crate edgelet_iothub;
extern crate env_logger;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate iothubservice;
#[macro_use]
extern crate log;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate tokio_core;
extern crate url;

mod error;
mod logging;
mod settings;

use std::convert::AsRef;

use clap::{App, Arg};
use failure::Fail;
use futures::{future, Future, Stream};
use hyper::Client as HyperClient;
use hyper::server::Http;
use tokio_core::reactor::{Core, Handle};
use url::Url;

use error::Error;
use settings::{Provisioning, Settings};
use edgelet_core::provisioning::{ManualProvisioning, Provision};
use edgelet_core::crypto::{DerivedKeyStore, KeyStore, MemoryKey};
use edgelet_docker::DockerModuleRuntime;
use edgelet_http::{ApiVersionService, API_VERSION};
use edgelet_http::logging::LoggingService;
use edgelet_http_mgmt::ManagementService;
use edgelet_http_workload::WorkloadService;
use edgelet_iothub::HubIdentityManager;
use iothubservice::{Client as HttpClient, DeviceClient};

fn main() {
    logging::init();
    ::std::process::exit(match main_runner() {
        Ok(_) => 0,
        Err(err) => {
            log_error(&err);
            1
        }
    });
}

fn main_runner() -> Result<(), Error> {
    let matches = App::new("edgelet")
        .arg(
            Arg::with_name("config-file")
                .short("c")
                .long("config-file")
                .value_name("FILE")
                .help("Sets an edgelet config file")
                .takes_value(true),
        )
        .get_matches();

    let config_file = matches
        .value_of("config-file")
        .and_then(|name| {
            info!("Using config file: {}", name);
            Some(name)
        })
        .or_else(|| {
            info!("Using default configuration");
            None
        });

    let settings = Settings::new(config_file)?;
    let provisioning_settings = settings.provisioning();
    let provisioning_result = provision(provisioning_settings)?;
    let key_store = provisioning_result;

    let mut core = Core::new()?;
    start_management("0.0.0.0:8080", key_store.clone(), &core.handle())?;
    start_workload("0.0.0.0:8081", &key_store, &core.handle())?;
    core.run(future::empty::<(), Error>())
}

fn provision(provisioning: &Provisioning) -> Result<DerivedKeyStore<MemoryKey>, Error> {
    let key_store = match *provisioning {
        Provisioning::Manual {
            ref device_connection_string,
        } => {
            let provision = ManualProvisioning::new(device_connection_string.as_str())?;
            let root_key = provision.key()?;
            info!(
                "Manually provisioning with DeviceId({}) and HostName({})",
                provision.device_id(),
                provision.host_name(),
            );
            DerivedKeyStore::new(MemoryKey::new(root_key))
        }
        Provisioning::Dps { .. } => DerivedKeyStore::new(MemoryKey::new("no dps")),
    };
    Ok(key_store)
}

fn start_management<K>(addr: &str, key_store: K, handle: &Handle) -> Result<(), Error>
where
    K: 'static + KeyStore + Clone,
    K::Key: AsRef<[u8]>,
{
    let uri = addr.parse()?;
    let client_handle = handle.clone();
    let server_handle = handle.clone();

    let docker = Url::parse("unix:///var/run/docker.sock")?;
    let mgmt = DockerModuleRuntime::new(&docker, &client_handle)?;

    let hyper_client = HyperClient::new(&client_handle);
    let http_client = HttpClient::new(
        hyper_client,
        API_VERSION,
        Url::parse("http://HUB_NAME.azure-devices.net")?,
    )?;
    let device_client = DeviceClient::new(http_client, "DEVICE_ID")?;
    let id_man = HubIdentityManager::new(key_store, device_client);

    let service = LoggingService::new(ApiVersionService::new(ManagementService::new(
        &mgmt,
        &id_man,
    )?));

    let serve = Http::new().serve_addr_handle(&uri, &server_handle, service)?;

    info!(
        "Listening on http://{} with 1 thread for management API.",
        serve.incoming_ref().local_addr()
    );

    let h2 = server_handle.clone();
    server_handle.spawn(
        serve
            .for_each(move |conn| {
                h2.spawn(
                    conn.map(|_| ())
                        .map_err(|err| error!("serve error: {:?}", err)),
                );
                Ok(())
            })
            .map_err(|_| ()),
    );
    Ok(())
}

fn start_workload<K>(addr: &str, key_store: &K, handle: &Handle) -> Result<(), Error>
where
    K: 'static + KeyStore + Clone,
{
    let uri = addr.parse()?;
    let server_handle = handle.clone();
    let service = LoggingService::new(ApiVersionService::new(WorkloadService::new(key_store)?));

    let serve = Http::new().serve_addr_handle(&uri, &server_handle, service)?;

    info!(
        "Listening on http://{} with 1 thread for workload API.",
        serve.incoming_ref().local_addr()
    );

    let h2 = server_handle.clone();
    server_handle.spawn(
        serve
            .for_each(move |conn| {
                h2.spawn(
                    conn.map(|_| ())
                        .map_err(|err| error!("serve error: {:?}", err)),
                );
                Ok(())
            })
            .map_err(|_| ()),
    );
    Ok(())
}

fn log_error(error: &Error) {
    let mut fail: &Fail = error;
    error!("{}", error.to_string());
    while let Some(cause) = fail.cause() {
        error!("\tcaused by: {}", cause.to_string());
        fail = cause;
    }
}
