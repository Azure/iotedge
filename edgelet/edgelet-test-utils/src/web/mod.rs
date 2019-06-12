// Copyright (c) Microsoft. All rights reserved.

#[cfg(windows)]
mod windows;

#[cfg(windows)]
pub use self::windows::run_pipe_server;

use std::fs;
use std::io;
#[cfg(unix)]
use std::os::unix::net::UnixListener as StdUnixListener;

use futures::prelude::*;
use hyper::server::conn::Http;
use hyper::service::service_fn;
use hyper::{self, Body, Request, Response};
#[cfg(unix)]
use hyperlocal::server::{Http as UdsHttp, Incoming as UdsIncoming};
#[cfg(windows)]
use hyperlocal_windows::server::{Http as UdsHttp, Incoming as UdsIncoming};
#[cfg(windows)]
use mio_uds_windows::net::UnixListener as StdUnixListener;

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
            })
            .flatten()
    })
}

pub fn run_uds_server<F, R>(path: &str, handler: F) -> impl Future<Item = (), Error = io::Error>
where
    F: 'static + Fn(Request<Body>) -> R + Clone + Send + Sync,
    R: 'static + Future<Item = Response<Body>, Error = io::Error> + Send,
{
    fs::remove_file(&path).unwrap_or(());

    // Bind a listener synchronously, so that the caller's client will not fail to connect
    // regardless of when the asynchronous server accepts the connection
    let listener = StdUnixListener::bind(path).unwrap();
    let incoming = UdsIncoming::from_std(listener, &Default::default()).unwrap();
    let serve = UdsHttp::new().serve_incoming(incoming, move || service_fn(handler.clone()));

    serve.for_each(|connecting| {
        connecting
            .then(|connection| {
                let connection = connection.unwrap();
                Ok::<_, hyper::Error>(connection)
            })
            .flatten()
            .map_err(|e| {
                io::Error::new(
                    io::ErrorKind::Other,
                    format!("failed to serve connection: {}", e),
                )
            })
    })
}
