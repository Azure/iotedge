// Copyright (c) Microsoft. All rights reserved.

use failure::{Compat, Fail};
use futures::future::FutureResult;
use futures::{future, Future, IntoFuture};
use hyper::service::{NewService, Service};
use hyper::{header, Body, Method, Request, Response, StatusCode};
use log::{debug, info};

use crate::{logging, Error};

#[derive(Clone)]
pub struct ApiService;

impl ApiService {
    pub fn new() -> Self {
        ApiService
    }

    fn handle(req: &Request<Body>) -> Result<Response<Body>, Error> {
        match (req.method(), req.uri().path()) {
            (&Method::GET, "/health") => Ok(Response::new(Body::empty())),
            _ => Ok(Response::builder()
                .status(StatusCode::NOT_FOUND)
                .body(Body::empty())
                .unwrap()),
        }
    }
}

impl Service for ApiService {
    type ReqBody = Body;
    type ResBody = Body;
    type Error = Compat<Error>;
    type Future = Box<dyn Future<Item = Response<Self::ResBody>, Error = Self::Error> + Send>;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        let request = format!("{} {} {:?}", req.method(), req.uri(), req.version());
        debug!("Starting api request {}", request);

        let fut = Self::handle(&req)
            .into_future()
            .map_err(|err: Error| {
                logging::failure(&err);
                err.compat()
            })
            .map(move |res| {
                debug!("Finished api request {}", request);

                let body_length = res
                    .headers()
                    .get(header::CONTENT_LENGTH)
                    .and_then(|length| length.to_str().ok().map(ToString::to_string))
                    .unwrap_or_else(|| "-".to_string());

                info!("\"{}\" {} {}", request, res.status(), body_length);
                res
            });

        Box::new(fut)
    }
}

impl NewService for ApiService {
    type ReqBody = Body;
    type ResBody = Body;
    type Error = Compat<Error>;
    type Service = Self;
    type Future = FutureResult<Self::Service, Self::InitError>;
    type InitError = Compat<Error>;

    fn new_service(&self) -> Self::Future {
        future::ok(self.clone())
    }
}
