// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use edgelet_http::UrlConnector;
use edgelet_test_utils::run_tcp_server;
use futures::future;
use futures::prelude::*;
use hyper::{Body, Client, Error as HyperError, Method, Request, Response, StatusCode};
#[cfg(unix)]
use typed_headers::mime;
use typed_headers::{ContentLength, ContentType, HeaderMapExt};
use url::Url;

const GET_RESPONSE: &str = "Yo";

fn hello_handler(_: Request<Body>) -> impl Future<Item = Response<Body>, Error = HyperError> {
    let mut response = Response::new(GET_RESPONSE.into());
    response
        .headers_mut()
        .typed_insert(&ContentLength(GET_RESPONSE.len() as u64));
    future::ok(response)
}

#[test]
fn tcp_get() {
    let (server, port) = run_tcp_server("127.0.0.1", hello_handler);
    let server = server.map_err(|err| panic!(err));

    let url = format!("http://localhost:{}", port);
    let connector = UrlConnector::new(&Url::parse(&url).unwrap()).unwrap();

    let client = Client::builder().build::<_, Body>(connector);
    let task = client
        .get(url.parse().unwrap())
        .and_then(|res| {
            assert_eq!(StatusCode::OK, res.status());
            res.into_body().concat2()
        })
        .map(|body| {
            assert_eq!(GET_RESPONSE, &String::from_utf8_lossy(body.as_ref()));
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

const POST_BODY: &str = r#"{"donuts":"yes"}"#;

fn post_handler(
    req: Request<Body>,
) -> Box<dyn Future<Item = Response<Body>, Error = HyperError> + Send> {
    // verify that the request body is what we expect
    Box::new(
        req.into_body()
            .concat2()
            .and_then(|body| {
                assert_eq!(POST_BODY, &String::from_utf8_lossy(body.as_ref()));
                Ok(())
            })
            .map(|_| Response::new(Body::empty())),
    )
}

#[test]
fn tcp_post() {
    let (server, port) = run_tcp_server("127.0.0.1", post_handler);
    let server = server.map_err(|err| panic!(err));

    let url = format!("http://localhost:{}", port);
    let connector = UrlConnector::new(&Url::parse(&url).unwrap()).unwrap();

    let client = Client::builder().build::<_, Body>(connector);

    let mut req = Request::builder()
        .method(Method::POST)
        .uri(url)
        .body(POST_BODY.into())
        .expect("could not build hyper::Request");
    req.headers_mut()
        .typed_insert(&ContentType(mime::APPLICATION_JSON));
    req.headers_mut()
        .typed_insert(&ContentLength(POST_BODY.len() as u64));

    let task = client.request(req).map(|res| {
        assert_eq!(StatusCode::OK, res.status());
    });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}
