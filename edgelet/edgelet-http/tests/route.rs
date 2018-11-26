// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]

extern crate edgelet_http;
extern crate futures;
extern crate hyper;

use futures::{future, Future, Stream};
use hyper::service::{NewService, Service};
use hyper::{Body, Chunk, Request, Response, StatusCode};

use edgelet_http::route::{Builder, Parameters, RegexRoutesBuilder, Router};
use edgelet_http::Error as HttpError;

#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
fn route1(
    _req: Request<Body>,
    params: Parameters,
) -> Box<Future<Item = Response<Body>, Error = HttpError> + Send> {
    let response = params.name("name").map_or_else(
        || {
            Response::builder()
                .status(StatusCode::BAD_REQUEST)
                .body(Body::default())
                .unwrap()
        },
        |name| {
            Response::builder()
                .status(StatusCode::OK)
                .body(format!("route1 {}", name).into())
                .unwrap()
        },
    );
    Box::new(future::ok(response))
}

#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
fn route2(
    _req: Request<Body>,
    params: Parameters,
) -> Box<Future<Item = Response<Body>, Error = HttpError> + Send> {
    let response = params.name("name").map_or_else(
        || {
            Response::builder()
                .status(StatusCode::BAD_REQUEST)
                .body(Body::default())
                .unwrap()
        },
        |name| {
            Response::builder()
                .status(StatusCode::CREATED)
                .body(format!("route2 {}", name).into())
                .unwrap()
        },
    );
    Box::new(future::ok(response))
}

#[test]
fn simple_route() {
    let recognizer = RegexRoutesBuilder::default()
        .get("/route1/(?P<name>[^/]+)", route1)
        .get("/route2/(?P<name>[^/]+)", route2)
        .finish();
    let router = Router::from(recognizer);
    let mut service = router.new_service().wait().unwrap();

    let uri1 = "http://example.com/route1/thename";
    let request1 = Request::get(uri1).body(Body::default()).unwrap();

    let uri2 = "http://example.com/route2/thename2";
    let request2 = Request::get(uri2).body(Body::default()).unwrap();

    let response1 = service.call(request1).wait().unwrap();
    let response2 = service.call(request2).wait().unwrap();

    let body1: String = response1
        .into_body()
        .concat2()
        .and_then(|body: Chunk| Ok(String::from_utf8(body.to_vec()).unwrap()))
        .wait()
        .unwrap();
    let body2: String = response2
        .into_body()
        .concat2()
        .and_then(|body: Chunk| Ok(String::from_utf8(body.to_vec()).unwrap()))
        .wait()
        .unwrap();

    assert_eq!("route1 thename", body1);
    assert_eq!("route2 thename2", body2);
}

#[test]
fn not_found() {
    let recognizer = RegexRoutesBuilder::default()
        .get("/route1/(?P<name>[^/]+)", route1)
        .get("/route2/(?P<name>[^/]+)", route2)
        .finish();
    let router = Router::from(recognizer);
    let mut service = router.new_service().wait().unwrap();

    let uri1 = "http://example.com/route3/thename";
    let request1 = Request::get(uri1).body(Body::default()).unwrap();

    let response1 = service.call(request1).wait().unwrap();

    assert_eq!(StatusCode::NOT_FOUND, response1.status());
}
