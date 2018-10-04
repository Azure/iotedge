// Copyright (c) Microsoft. All rights reserved.

use edgelet_http::route::{Handler, Parameters};
use futures::{future, Future};
use http::{Request, Response};
use hyper::{Body, Error as HyperError};

pub struct GetModule;

impl Handler<Parameters> for GetModule {
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> Box<Future<Item = Response<Body>, Error = HyperError> + Send> {
        let response = Response::new(Body::default());
        Box::new(future::ok(response))
    }
}
