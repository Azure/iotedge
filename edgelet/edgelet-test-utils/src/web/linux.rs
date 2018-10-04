// Copyright (c) Microsoft. All rights reserved.

use std::fs;
use std::io;
use std::os::unix::net::UnixListener as StdUnixListener;

use futures::prelude::*;
use hyper::service::service_fn;
use hyper::{self, Body, Request, Response};
use hyperlocal::server::{Http as UdsHttp, Incoming as UdsIncoming};

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
            }).flatten()
            .map_err(|e| {
                io::Error::new(
                    io::ErrorKind::Other,
                    format!("failed to serve connection: {}", e),
                )
            })
    })
}
