// Copyright (c) Microsoft. All rights reserved.

#[macro_use]
extern crate edgelet_http;
extern crate futures;
extern crate http;
extern crate hyper;
extern crate tokio;

use edgelet_http::route::{Builder, Parameters, Router};
use edgelet_http::HyperExt;
use futures::{future, Future};
use http::header::CONTENT_TYPE;
use http::{Request, Response, StatusCode};
use hyper::server::conn::Http;
use hyper::{Body, Error as HyperError};

fn index(
    _req: Request<Body>,
    _params: Parameters,
) -> Box<Future<Item = Response<Body>, Error = HyperError> + Send> {
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
) -> Box<Future<Item = Response<Body>, Error = HyperError> + Send> {
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
) -> Box<Future<Item = Response<Body>, Error = HyperError> + Send> {
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
) -> Box<Future<Item = Response<Body>, Error = HyperError> + Send> {
    let response = Response::builder()
        .status(StatusCode::BAD_REQUEST)
        .body(Body::default())
        .unwrap();
    Box::new(future::ok(response))
}

fn main() {
    let router = router!(
        get "/" => index,
        get "/identities" => identities_list,
        put "/identities/(?P<name>[^/]+)" => identities_update,
        delete "/identities/(?P<name>[^/]+)" => identities_delete,
    );

    let addr = "tcp://0.0.0.0:8080".parse().unwrap();

    println!("Starting server on {}", addr);
    let run = Http::new().bind_url(addr, router).unwrap().run();

    tokio::runtime::current_thread::Runtime::new()
        .unwrap()
        .block_on(run)
        .unwrap();
}
