// Copyright (c) Microsoft. All rights reserved.

#[cfg(unix)]
mod linux;

#[cfg(windows)]
mod windows;

#[cfg(unix)]
pub use self::linux::run_uds_server;

#[cfg(windows)]
pub use self::windows::run_pipe_server;

use futures::prelude::*;
use hyper::server::conn::Http;
use hyper::service::service_fn;
use hyper::{self, Body, Request, Response};

pub fn run_tcp_server<F, R>(
    ip: &str,
    port: u16,
    handler: F,
) -> impl Future<Item = (), Error = hyper::Error>
where
    F: 'static + Fn(Request<Body>) -> R + Clone + Send + Sync,
    R: 'static + Future<Item = Response<Body>, Error = hyper::Error> + Send,
{
    let addr = &format!("{}:{}", ip, port).parse().unwrap();

    let serve = Http::new()
        .serve_addr(addr, move || service_fn(handler.clone()))
        .unwrap();
    serve.for_each(|connecting| {
        connecting
            .then(|connection| {
                let connection = connection.unwrap();
                Ok::<_, hyper::Error>(connection)
            }).flatten()
    })
}
