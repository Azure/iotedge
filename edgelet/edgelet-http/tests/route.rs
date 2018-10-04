// Copyright (c) Microsoft. All rights reserved.

extern crate edgelet_http;
extern crate futures;
extern crate http;
extern crate hyper;
extern crate regex;

use edgelet_http::route::{Builder, Parameters, RegexRoutesBuilder, Router};
use futures::{future, Future, Stream};
use http::{Request, Response, StatusCode};
use hyper::service::{NewService, Service};
use hyper::{Body, Chunk, Error as HyperError};

fn route1(
    _req: Request<Body>,
    params: Parameters,
) -> Box<Future<Item = Response<Body>, Error = HyperError> + Send> {
    let response = params
        .name("name")
        .map(|name| {
            Response::builder()
                .status(StatusCode::OK)
                .body(format!("route1 {}", name).into())
                .unwrap()
        }).unwrap_or_else(|| {
            Response::builder()
                .status(StatusCode::BAD_REQUEST)
                .body(Body::default())
                .unwrap()
        });
    Box::new(future::ok(response))
}

fn route2(
    _req: Request<Body>,
    params: Parameters,
) -> Box<Future<Item = Response<Body>, Error = HyperError> + Send> {
    let response = params
        .name("name")
        .map(|name| {
            Response::builder()
                .status(StatusCode::CREATED)
                .body(format!("route2 {}", name).into())
                .unwrap()
        }).unwrap_or_else(|| {
            Response::builder()
                .status(StatusCode::BAD_REQUEST)
                .body(Body::default())
                .unwrap()
        });
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
