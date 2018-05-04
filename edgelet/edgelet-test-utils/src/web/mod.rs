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
use hyper::Error as HyperError;
use hyper::server::{Http, Request, Response, Service};

struct RequestHandler<F, R>
where
    R: 'static + Future<Item = Response, Error = HyperError>,
    F: Fn(Request) -> R,
{
    handler: Box<F>,
}

impl<F, R> RequestHandler<F, R>
where
    R: 'static + Future<Item = Response, Error = HyperError>,
    F: Fn(Request) -> R,
{
    pub fn new(handler: F) -> RequestHandler<F, R> {
        RequestHandler {
            handler: Box::new(handler),
        }
    }
}

impl<F, R> Service for RequestHandler<F, R>
where
    R: 'static + Future<Item = Response, Error = HyperError>,
    F: Fn(Request) -> R,
{
    type Request = Request;
    type Response = Response;
    type Error = HyperError;
    type Future = Box<Future<Item = Self::Response, Error = Self::Error>>;

    fn call(&self, req: Request) -> Self::Future {
        let handler = &self.handler;
        Box::new(handler(req))
    }
}

pub fn run_tcp_server<F, R>(ip: &str, port: u16, handler: &'static F, ready_channel: &Sender<()>)
where
    R: 'static + Future<Item = Response, Error = HyperError>,
    F: Fn(Request) -> R,
{
    let addr = &format!("{}:{}", ip, port).parse().unwrap();
    let server = Http::new()
        .bind(addr, move || Ok(RequestHandler::new(handler)))
        .unwrap();

    // signal that the server is ready to run
    ready_channel.send(()).unwrap();

    server.run().unwrap();
}
