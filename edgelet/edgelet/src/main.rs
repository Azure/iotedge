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
extern crate log;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate tokio_core;
extern crate url;

mod settings;
mod error;

use std::convert::AsRef;

use clap::{App, Arg};
use edgelet_core::crypto::{DerivedKeyStore, KeyStore, MemoryKey};
use edgelet_docker::DockerModuleRuntime;
use edgelet_http::{ApiVersionService, API_VERSION};
use edgelet_http::logging::LoggingService;
use edgelet_http_mgmt::ManagementService;
use edgelet_http_workload::WorkloadService;
use edgelet_iothub::HubIdentityManager;
use futures::{future, Future, Stream};
use hyper::Client as HyperClient;
use hyper::server::Http;
use iothubservice::{Client as HttpClient, DeviceClient};
use tokio_core::reactor::{Core, Handle};
use url::Url;

use error::Error;
use settings::Settings;

fn main() {
    env_logger::init();
    ::std::process::exit(match main_runner() {
        Ok(_) => 0,
        Err(err) => {
            eprintln!("error: {:?}", err);
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
            println!("Using config file: {}", name);
            Some(name)
        })
        .or_else(|| {
            println!("Using default configuration");
            None
        });

    let _settings = Settings::new(config_file)?;

    let root_key = MemoryKey::new("key");
    let key_store = DerivedKeyStore::new(root_key);

    let mut core = Core::new().unwrap();
    start_management("0.0.0.0:8080", key_store.clone(), &core.handle());
    start_workload("0.0.0.0:8081", key_store, &core.handle());
    core.run(future::empty::<(), ()>()).unwrap();
    Ok(())
}

fn start_management<K>(addr: &str, key_store: K, handle: &Handle)
where
    K: 'static + KeyStore + Clone,
    K::Key: AsRef<[u8]>,
{
    let uri = addr.parse().unwrap();
    let client_handle = handle.clone();
    let server_handle = handle.clone();

    let docker = Url::parse("unix:///var/run/docker.sock").unwrap();
    let mgmt = DockerModuleRuntime::new(&docker, &client_handle).unwrap();

    let hyper_client = HyperClient::new(&client_handle);
    let http_client = HttpClient::new(
        hyper_client,
        API_VERSION,
        Url::parse("http://HUB_NAME.azure-devices.net").unwrap(),
    ).unwrap();
    let device_client = DeviceClient::new(http_client, "DEVICE_ID").unwrap();
    let id_man = HubIdentityManager::new(key_store, device_client);

    let service = LoggingService::new(ApiVersionService::new(
        ManagementService::new(&mgmt, &id_man).unwrap(),
    ));

    let serve = Http::new()
        .serve_addr_handle(&uri, &server_handle, service)
        .unwrap();

    println!(
        "Listening on http://{} with 1 thread for management API.",
        serve.incoming_ref().local_addr()
    );

    let h2 = server_handle.clone();
    server_handle.spawn(
        serve
            .for_each(move |conn| {
                h2.spawn(
                    conn.map(|_| ())
                        .map_err(|err| println!("serve error: {:?}", err)),
                );
                Ok(())
            })
            .map_err(|_| ()),
    );
}

fn start_workload<K>(addr: &str, key_store: K, handle: &Handle)
where
    K: 'static + KeyStore + Clone,
{
    let uri = addr.parse().unwrap();
    let server_handle = handle.clone();
    let service = LoggingService::new(ApiVersionService::new(
        WorkloadService::new(key_store).unwrap(),
    ));

    let serve = Http::new()
        .serve_addr_handle(&uri, &server_handle, service)
        .unwrap();

    println!(
        "Listening on http://{} with 1 thread for workload API.",
        serve.incoming_ref().local_addr()
    );

    let h2 = server_handle.clone();
    server_handle.spawn(
        serve
            .for_each(move |conn| {
                h2.spawn(
                    conn.map(|_| ())
                        .map_err(|err| println!("serve error: {:?}", err)),
                );
                Ok(())
            })
            .map_err(|_| ()),
    );
}
