// Copyright (c) Microsoft. All rights reserved.

#[cfg(unix)]
mod linux;

#[cfg(windows)]
mod windows;

#[cfg(unix)]
pub use self::linux::run_uds_server;

#[cfg(windows)]
pub use self::windows::run_pipe_server;

use std::sync::mpsc::Sender;

use futures::prelude::*;
use hyper::server::{const_service, service_fn, Http, Request, Response};
use hyper::Error as HyperError;

pub fn run_tcp_server<F, R>(ip: &str, port: u16, handler: F, ready_channel: &Sender<()>)
where
    R: 'static + Future<Item = Response, Error = HyperError>,
    F: 'static + Fn(Request) -> R,
{
    let addr = &format!("{}:{}", ip, port).parse().unwrap();
    let server = Http::new()
        .bind(addr, const_service(service_fn(handler)))
        .unwrap();

    // signal that the server is ready to run
    ready_channel.send(()).unwrap();

    server.run().unwrap();
}
