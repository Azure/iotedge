// Copyright (c) Microsoft. All rights reserved.

#![cfg(windows)]
#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use std::io;

use futures::{future, Future, Stream};
use hyper::{Body, Client as HyperClient, Method, Request, Response, StatusCode, Uri as HyperUri};
use rand::Rng;
use typed_headers::{mime, ContentLength, ContentType, HeaderMapExt};

use edgelet_test_utils::run_pipe_server;
use hyper_named_pipe::{PipeConnector, Uri};

fn make_path() -> String {
    format!(r"\\.\pipe\my-pipe-{}", rand::thread_rng().gen::<u64>())
}

fn make_url(path: &str) -> Uri {
    Uri::new(&format!("npipe:{}", path.replace("\\", "/")), "/").unwrap()
}

#[allow(clippy::needless_pass_by_value)]
fn get_handler(_req: Request<Body>) -> impl Future<Item = Response<Body>, Error = io::Error> {
    future::ok(Response::new(Body::default()))
}

#[test]
fn get() {
    let path = make_path();
    let url = make_url(&path);

    let server = run_pipe_server(path.into(), get_handler).map_err(|err| eprintln!("{}", err));

    let hyper_client = HyperClient::builder().build::<_, Body>(PipeConnector);

    // make a get request
    let task = hyper_client.get(url.into());

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    let response = runtime.block_on(task).unwrap();
    assert_eq!(response.status(), StatusCode::OK);
}

const GET_RESPONSE: &str = "The answer is 42";

#[allow(clippy::needless_pass_by_value)]
fn get_with_body_handler(
    _req: Request<Body>,
) -> impl Future<Item = Response<Body>, Error = io::Error> {
    let response = Response::builder()
        .header(hyper::header::CONTENT_TYPE, "text/plain; charset=utf-8")
        .header(
            hyper::header::CONTENT_LENGTH,
            format!("{}", GET_RESPONSE.len()),
        )
        .body(GET_RESPONSE.into())
        .expect("couldn't create response body");
    future::ok(response)
}

#[test]
fn get_with_body() {
    let path = make_path();
    let url = make_url(&path);

    let server =
        run_pipe_server(path.into(), get_with_body_handler).map_err(|err| eprintln!("{}", err));

    let hyper_client = HyperClient::builder().build::<_, Body>(PipeConnector);

    // make a get request
    let task = hyper_client
        .get(url.into())
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

fn post_handler(req: Request<Body>) -> impl Future<Item = Response<Body>, Error = io::Error> {
    req.into_body().concat2().then(|body| {
        let body = body.expect("couldn't read request body");
        let body = String::from_utf8_lossy(&body);
        assert_eq!(&body, POST_BODY);
        Ok(Response::new(Body::default()))
    })
}

#[test]
fn post() {
    let path = make_path();
    let url: HyperUri = make_url(&path).into();

    let server = run_pipe_server(path.into(), post_handler).map_err(|err| eprintln!("{}", err));

    let hyper_client = HyperClient::builder().build::<_, Body>(PipeConnector);

    // make a post request
    let mut req = Request::builder()
        .method(Method::POST)
        .uri(url)
        .body(POST_BODY.into())
        .expect("could not build hyper::Request");
    req.headers_mut()
        .typed_insert(&ContentType(mime::APPLICATION_JSON));
    req.headers_mut()
        .typed_insert(&ContentLength(POST_BODY.len() as u64));

    let task = hyper_client.request(req).map(|res| {
        assert_eq!(StatusCode::OK, res.status());
    });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}
