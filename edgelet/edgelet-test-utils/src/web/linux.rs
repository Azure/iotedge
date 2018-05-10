// Copyright (c) Microsoft. All rights reserved.

#[cfg(unix)]
use super::*;

use std::fs;
use std::sync::mpsc::Sender;

use futures::prelude::*;
use hyper::Error as HyperError;
use hyper::server::{Request, Response};
use hyperlocal::server::Http as UdsHttp;

pub fn run_uds_server<F, R>(path: &str, handler: &'static F, ready_channel: &Sender<()>)
where
    R: 'static + Future<Item = Response, Error = HyperError>,
    F: Fn(Request) -> R + Send + Sync,
{
    fs::remove_file(&path).unwrap_or(());

    let server = UdsHttp::new()
        .bind(path, move || Ok(RequestHandler::new(handler)))
        .unwrap();

    // signal that the server is ready to run
    ready_channel.send(()).unwrap();

    server.run().unwrap();
}
