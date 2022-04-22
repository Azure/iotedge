// Copyright (c) Microsoft. All rights reserved.

use futures::{future, Future};
use hyper::{Body, Request, Response};

use edgelet_http::route::{Handler, Parameters};

pub struct GetModule;

impl Handler<Parameters> for GetModule {
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = anyhow::Error> + Send> {
        let response = Response::new(Body::default());
        Box::new(future::ok(response))
    }
}
