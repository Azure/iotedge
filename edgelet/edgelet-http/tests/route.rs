// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

extern crate edgelet_http;
extern crate futures;
extern crate hyper;

use futures::{future, Future, Stream};
use hyper::service::{NewService, Service};
use hyper::{Body, Chunk, Request, Response, StatusCode};

use edgelet_http::route::{Builder, Parameters, RegexRoutesBuilder, Router};
use edgelet_http::Error as HttpError;
use edgelet_http::Version;

#[allow(clippy::needless_pass_by_value)]
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

#[allow(clippy::needless_pass_by_value)]
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
        .get(
            Version::Version2018_06_28,
            "/route1/(?P<name>[^/]+)",
            route1,
        )
        .get(
            Version::Version2018_06_28,
            "/route2/(?P<name>[^/]+)",
            route2,
        )
        .finish();
    let router = Router::from(recognizer);
    let mut service = router.new_service().wait().unwrap();

    let uri1 = "http://example.com/route1/thename?api-version=2018-06-28";
    let request1 = Request::get(uri1).body(Body::default()).unwrap();

    let uri2 = "http://example.com/route2/thename2?api-version=2018-06-28";
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
fn same_path_with_different_version() {
    let recognizer = RegexRoutesBuilder::default()
        .get(
            Version::Version2018_06_28,
            "/route1/(?P<name>[^/]+)",
            route1,
        )
        .get(
            Version::Version2019_01_30,
            "/route1/(?P<name>[^/]+)",
            route2,
        )
        .finish();
    let router = Router::from(recognizer);
    let mut service = router.new_service().wait().unwrap();

    let uri1 = "http://example.com/route1/thename?api-version=2018-06-28";
    let request1 = Request::get(uri1).body(Body::default()).unwrap();

    let uri2 = "http://example.com/route1/thename2?api-version=2019-01-30";
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
    assert_eq!("route1 thename2", body2);
}

#[test]
fn not_found() {
    let recognizer = RegexRoutesBuilder::default()
        .get(
            Version::Version2018_06_28,
            "/route1/(?P<name>[^/]+)",
            route1,
        )
        .get(
            Version::Version2018_06_28,
            "/route2/(?P<name>[^/]+)",
            route2,
        )
        .finish();
    let router = Router::from(recognizer);
    let mut service = router.new_service().wait().unwrap();

    let uri1 = "http://example.com/route3/thename?api-version=2018-06-28";
    let request1 = Request::get(uri1).body(Body::default()).unwrap();

    let response1 = service.call(request1).wait().unwrap();

    assert_eq!(StatusCode::NOT_FOUND, response1.status());
}

#[test]
fn api_version_does_not_exist() {
    let recognizer = RegexRoutesBuilder::default()
        .get(
            Version::Version2018_06_28,
            "/route1/(?P<name>[^/]+)",
            route1,
        )
        .get(
            Version::Version2018_06_28,
            "/route2/(?P<name>[^/]+)",
            route2,
        )
        .finish();
    let router = Router::from(recognizer);
    let mut service = router.new_service().wait().unwrap();

    let uri1 = "http://example.com/route3/thename";
    let request1 = Request::get(uri1).body(Body::default()).unwrap();

    let response = service.call(request1).wait().unwrap();

    assert_eq!(StatusCode::BAD_REQUEST, response.status());
}

#[test]
fn api_version_is_unsupported() {
    let url = "http://localhost?api-version=not-a-valid-version";
    let recognizer = RegexRoutesBuilder::default()
        .get(
            Version::Version2018_06_28,
            "/route1/(?P<name>[^/]+)",
            route1,
        )
        .get(
            Version::Version2018_06_28,
            "/route2/(?P<name>[^/]+)",
            route2,
        )
        .finish();
    let router = Router::from(recognizer);
    let mut service = router.new_service().wait().unwrap();

    let request1 = Request::get(url).body(Body::default()).unwrap();
    let response = service.call(request1).wait().unwrap();

    assert_eq!(StatusCode::BAD_REQUEST, response.status());
}

#[test]
fn not_found_for_api_version() {
    let url = "http://localhost?api-version=2018-06-28";
    let recognizer = RegexRoutesBuilder::default()
        .get(
            Version::Version2018_06_28,
            "/route1/(?P<name>[^/]+)",
            route1,
        )
        .get(
            Version::Version2019_01_30,
            "/route2/(?P<name>[^/]+)",
            route2,
        )
        .finish();
    let router = Router::from(recognizer);
    let mut service = router.new_service().wait().unwrap();

    let request1 = Request::get(url).body(Body::default()).unwrap();
    let response = service.call(request1).wait().unwrap();

    assert_eq!(StatusCode::NOT_FOUND, response.status());
}
