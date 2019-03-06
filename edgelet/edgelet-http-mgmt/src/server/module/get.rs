// Copyright (c) Microsoft. All rights reserved.

use futures::{future, Future};
use hyper::{Body, Request, Response};

use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;

pub struct GetModule;

impl Handler<Parameters> for GetModule {
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        let response = Response::new(Body::default());
        Box::new(future::ok(response))
    }
}
