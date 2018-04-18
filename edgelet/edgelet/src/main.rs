// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate clap;
extern crate config;
extern crate docker_mri;
extern crate edgelet_http_mgmt;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate tokio_core;
extern crate url;

mod settings;
mod error;

use clap::{App, Arg};
use error::Error;

use settings::Settings;
use docker_mri::DockerModuleRuntime;
use edgelet_http_mgmt::{ApiVersionService, ManagementService};
use futures::{future, Future, Stream};
use hyper::server::Http;
use tokio_core::reactor::Core;
use url::Url;

fn main() {
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

    let addr = "0.0.0.0:8080".parse().unwrap();
    let mut core = Core::new().unwrap();

    let docker = Url::parse("unix:///var/run/docker.sock").unwrap();
    let mgmt = DockerModuleRuntime::new(&docker, &core.handle()).unwrap();
    let service = ApiVersionService::new(ManagementService::new(&mgmt).unwrap());

    let server_handle = core.handle();
    let serve = Http::new()
        .serve_addr_handle(&addr, &server_handle, service)
        .unwrap();
    println!(
        "Listening on http://{} with 1 thread.",
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
    core.run(future::empty::<(), ()>()).unwrap();
    Ok(())
}
