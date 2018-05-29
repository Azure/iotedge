// Copyright (c) Microsoft. All rights reserved.

use std::fs;
use std::sync::mpsc::Sender;

use futures::prelude::*;
use hyper::server::{const_service, service_fn, Request, Response};
use hyper::Error as HyperError;
use hyperlocal::server::Http as UdsHttp;

pub fn run_uds_server<F, R>(path: &str, handler: F, ready_channel: &Sender<()>)
where
    R: 'static + Future<Item = Response, Error = HyperError>,
    F: 'static + Fn(Request) -> R + Send + Sync,
{
    fs::remove_file(&path).unwrap_or(());

    let server = UdsHttp::new()
        .bind(path, const_service(service_fn(handler)))
        .unwrap();

    // signal that the server is ready to run
    ready_channel.send(()).unwrap();

    server.run().unwrap();
}
