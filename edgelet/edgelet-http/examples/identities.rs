// Copyright (c) Microsoft. All rights reserved.

#[macro_use]
extern crate edgelet_http;
extern crate futures;
extern crate http;
extern crate hyper;
extern crate tokio_core;

use edgelet_http::HyperExt;
use edgelet_http::route::{BoxFuture, Builder, Parameters, Router};
use futures::future;
use http::header::CONTENT_TYPE;
use http::{Request, Response, StatusCode};
use hyper::server::Http;
use hyper::{Body, Error as HyperError};
use tokio_core::reactor::Core;

fn index(_req: Request<Body>, _params: Parameters) -> BoxFuture<Response<Body>, HyperError> {
    let response = Response::builder()
        .status(StatusCode::OK)
        .header(CONTENT_TYPE, "text/plain")
        .body("index".into())
        .unwrap();
    Box::new(future::ok(response))
}

fn identities_list(
    _req: Request<Body>,
    _params: Parameters,
) -> BoxFuture<Response<Body>, HyperError> {
    let response = Response::builder()
        .status(StatusCode::OK)
        .header(CONTENT_TYPE, "application/json")
        .body(r#"{"identities":["moduleId":"edgeHub","managedBy":"iot-edge","generationId":"731f88d3-cf72-4a23-aca1-cd91fd4f52ff"}]}"#.into())
        .unwrap();
    Box::new(future::ok(response))
}

fn identities_update(
    _req: Request<Body>,
    params: Parameters,
) -> BoxFuture<Response<Body>, HyperError> {
    let response = params
        .name("name")
        .map(|name| {
            Response::builder()
                .status(StatusCode::OK)
                .header(CONTENT_TYPE, "application/json")
                .body(format!("{{\"moduleId\":\"{}\",\"managedBy\":\"iot-edge\",\"generationId\":\"731f88d3-cf72-4a23-aca1-cd91fd4f52ff\"}}", name).into())
                .unwrap()
        })
        .unwrap_or_else(|| {
            Response::builder()
                .status(StatusCode::BAD_REQUEST)
                .body(Body::default())
                .unwrap()
        });
    Box::new(future::ok(response))
}

fn identities_delete(
    _req: Request<Body>,
    _params: Parameters,
) -> BoxFuture<Response<Body>, HyperError> {
    let response = Response::builder()
        .status(StatusCode::BAD_REQUEST)
        .body(Body::default())
        .unwrap();
    Box::new(future::ok(response))
}

fn main() {
    let mut core = Core::new().unwrap();
    let handle = core.handle();
    let router = router!(
        get "/" => index,
        get "/identities" => identities_list,
        put "/identities/(?P<name>[^/]+)" => identities_update,
        delete "/identities/(?P<name>[^/]+)" => identities_delete,
    );

    let addr = "tcp://0.0.0.0:8080".parse().unwrap();

    println!("Starting server on {}", addr);
    let run = Http::new().bind_handle(addr, handle, router).unwrap().run();
    core.run(run).unwrap();
}
