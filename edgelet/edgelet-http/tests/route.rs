// Copyright (c) Microsoft. All rights reserved.

extern crate edgelet_http;
extern crate futures;
extern crate hyper;
extern crate regex;

use std::str::FromStr;

use edgelet_http::route::{BoxFuture, Builder, Parameters, RegexRoutesBuilder, Router};
use futures::{future, Future, Stream};
use hyper::{Chunk, Error as HyperError, Method, Request, Response, StatusCode, Uri};
use hyper::server::{NewService, Service};

fn route1(_req: Request, params: Parameters) -> BoxFuture<Response, HyperError> {
    let response = params
        .name("name")
        .map(|name| {
            Response::new()
                .with_status(StatusCode::Ok)
                .with_body(format!("route1 {}", name))
        })
        .unwrap_or_else(|| Response::new().with_status(StatusCode::BadRequest));
    Box::new(future::ok(response))
}

fn route2(_req: Request, params: Parameters) -> BoxFuture<Response, HyperError> {
    let response = params
        .name("name")
        .map(|name| {
            Response::new()
                .with_status(StatusCode::Created)
                .with_body(format!("route2 {}", name))
        })
        .unwrap_or_else(|| Response::new().with_status(StatusCode::BadRequest));
    Box::new(future::ok(response))
}

#[test]
fn simple_route() {
    let recognizer = RegexRoutesBuilder::default()
        .get("/route1/(?P<name>[^/]+)", route1)
        .get("/route2/(?P<name>[^/]+)", route2)
        .finish();
    let router = Router::from(recognizer);
    let service = router.new_service().unwrap();

    let uri1 = Uri::from_str("http://example.com/route1/thename").unwrap();
    let request1 = Request::new(Method::Get, uri1);

    let uri2 = Uri::from_str("http://example.com/route2/thename2").unwrap();
    let request2 = Request::new(Method::Get, uri2);

    let response1 = service.call(request1).wait().unwrap();
    let response2 = service.call(request2).wait().unwrap();

    let body1: String = response1
        .body()
        .concat2()
        .and_then(|body: Chunk| Ok(String::from_utf8(body.to_vec()).unwrap()))
        .wait()
        .unwrap();
    let body2: String = response2
        .body()
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
    let service = router.new_service().unwrap();

    let uri1 = Uri::from_str("http://example.com/route3/thename").unwrap();
    let request1 = Request::new(Method::Get, uri1);

    let response1 = service.call(request1).wait().unwrap();

    assert_eq!(StatusCode::NotFound, response1.status());
}
