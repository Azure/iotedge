// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]

#[macro_use]
extern crate edgelet_http;
extern crate futures;
extern crate hyper;
extern crate tokio;

use edgelet_http::route::{Builder, Parameters, Router};
use edgelet_http::{Error as HttpError, HyperExt, Version};
use futures::{future, Future};
use hyper::header::CONTENT_TYPE;
use hyper::server::conn::Http;
use hyper::{Body, Request, Response, StatusCode};

#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
fn index(
    _req: Request<Body>,
    _params: Parameters,
) -> Box<Future<Item = Response<Body>, Error = HttpError> + Send> {
    let response = Response::builder()
        .status(StatusCode::OK)
        .header(CONTENT_TYPE, "text/plain")
        .body("index".into())
        .unwrap();
    Box::new(future::ok(response))
}

#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
fn identities_list(
    _req: Request<Body>,
    _params: Parameters,
) -> Box<Future<Item = Response<Body>, Error = HttpError> + Send> {
    let response = Response::builder()
        .status(StatusCode::OK)
        .header(CONTENT_TYPE, "application/json")
        .body(r#"{"identities":["moduleId":"edgeHub","managedBy":"iot-edge","generationId":"731f88d3-cf72-4a23-aca1-cd91fd4f52ff"}]}"#.into())
        .unwrap();
    Box::new(future::ok(response))
}

#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
fn identities_update(
    _req: Request<Body>,
    params: Parameters,
) -> Box<Future<Item = Response<Body>, Error = HttpError> + Send> {
    let response = params
        .name("name")
        .map_or_else(|| {
            Response::builder()
                .status(StatusCode::BAD_REQUEST)
                .body(Body::default())
                .unwrap()
        }, |name| {
            Response::builder()
                .status(StatusCode::OK)
                .header(CONTENT_TYPE, "application/json")
                .body(format!("{{\"moduleId\":\"{}\",\"managedBy\":\"iot-edge\",\"generationId\":\"731f88d3-cf72-4a23-aca1-cd91fd4f52ff\"}}", name).into())
                .unwrap()
        });
    Box::new(future::ok(response))
}

#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
fn identities_delete(
    _req: Request<Body>,
    _params: Parameters,
) -> Box<Future<Item = Response<Body>, Error = HttpError> + Send> {
    let response = Response::builder()
        .status(StatusCode::BAD_REQUEST)
        .body(Body::default())
        .unwrap();
    Box::new(future::ok(response))
}

fn main() {
    let router = router!(
        get    Version2018_06_28, "/" => index,
        get    Version2018_06_28, "/identities" => identities_list,
        put    Version2018_06_28, "/identities/(?P<name>[^/]+)" => identities_update,
        delete Version2018_06_28, "/identities/(?P<name>[^/]+)" => identities_delete,
    );

    let addr = "tcp://0.0.0.0:8080".parse().unwrap();

    println!("Starting server on {}", addr);
    let run = Http::new().bind_url(addr, router).unwrap().run();

    tokio::runtime::current_thread::Runtime::new()
        .unwrap()
        .block_on(run)
        .unwrap();
}
