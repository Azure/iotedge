// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate docker_mri;
extern crate edgelet_http_mgmt;
extern crate futures;
extern crate hyper;
extern crate tokio_core;
extern crate url;

use docker_mri::DockerModuleRuntime;
use edgelet_http_mgmt::ManagementService;
use futures::{future, Future, Stream};
use hyper::server::Http;
use tokio_core::reactor::Core;
use url::Url;

fn main() {
    let addr = "0.0.0.0:8080".parse().unwrap();
    let mut core = Core::new().unwrap();

    let docker = Url::parse("unix:///var/run/docker.sock").unwrap();
    let mgmt = DockerModuleRuntime::new(&docker, &core.handle()).unwrap();
    let service = ManagementService::new(mgmt).unwrap();

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
}
