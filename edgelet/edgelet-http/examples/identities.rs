// Copyright (c) Microsoft. All rights reserved.

#[macro_use]
extern crate edgelet_http;
extern crate futures;
extern crate hyper;

use edgelet_http::route::{BoxFuture, Builder, Parameters, Router};
use futures::future;
use hyper::{Error as HyperError, Request, Response, StatusCode};
use hyper::header::ContentType;
use hyper::server::Http;

fn index(_req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
    let response = Response::new()
        .with_status(StatusCode::Ok)
        .with_header(ContentType::plaintext())
        .with_body("index");
    Box::new(future::ok(response))
}

fn identities_list(_req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
    let response = Response::new()
        .with_status(StatusCode::Ok)
        .with_header(ContentType::json())
        .with_body(r#"{"identities":["moduleId":"edgeHub","managedBy":"iot-edge","generationId":"731f88d3-cf72-4a23-aca1-cd91fd4f52ff"}]}"#);
    Box::new(future::ok(response))
}

fn identities_update(_req: Request, params: Parameters) -> BoxFuture<Response, HyperError> {
    let response = params
        .name("name")
        .map(|name| {
            Response::new()
                .with_status(StatusCode::Ok)
                .with_header(ContentType::json())
                .with_body(format!("{{\"moduleId\":\"{}\",\"managedBy\":\"iot-edge\",\"generationId\":\"731f88d3-cf72-4a23-aca1-cd91fd4f52ff\"}}", name))
        })
        .unwrap_or_else(|| Response::new().with_status(StatusCode::BadRequest));
    Box::new(future::ok(response))
}

fn identities_delete(_req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
    let response = Response::new().with_status(StatusCode::Ok);
    Box::new(future::ok(response))
}

fn main() {
    let router = router!(
        get "/" => index,
        get "/identities" => identities_list,
        put "/identities/(?P<name>[^/]+)" => identities_update,
        delete "/identities/(?P<name>[^/]+)" => identities_delete,
    );

    let addr = "0.0.0.0:8080".parse().unwrap();

    println!("Starting server on {}", addr);
    Http::new().bind(&addr, router).unwrap().run().unwrap()
}
