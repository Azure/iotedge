// Copyright (c) Microsoft. All rights reserved.

use futures::future;
use hyper::Error as HyperError;
use hyper::server::{Request, Response};

use edgelet_http::route::{BoxFuture, Handler, Parameters};

pub struct ServerCertHandler;

impl Handler<Parameters> for ServerCertHandler {
    fn handle(&self, _req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = Response::new();
        Box::new(future::ok(response))
    }
}
