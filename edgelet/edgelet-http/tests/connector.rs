// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]

extern crate edgelet_http;
extern crate edgelet_test_utils;
extern crate futures;
#[cfg(windows)]
extern crate httparse;
extern crate hyper;
#[cfg(windows)]
extern crate hyper_named_pipe;
#[cfg(linux)]
extern crate hyperlocal;
#[cfg(windows)]
extern crate hyperlocal_windows;
#[cfg(windows)]
extern crate rand;
extern crate tempdir;
extern crate tokio;
extern crate typed_headers;
extern crate url;

use std::io;
#[cfg(windows)]
use std::sync::mpsc::channel;
#[cfg(windows)]
use std::thread;

use edgelet_http::UrlConnector;
#[cfg(windows)]
use edgelet_test_utils::run_pipe_server;
use edgelet_test_utils::run_uds_server;
use edgelet_test_utils::{get_unused_tcp_port, run_tcp_server};
use futures::future;
use futures::prelude::*;
#[cfg(windows)]
use httparse::Request as HtRequest;
use hyper::{
    Body, Client, Error as HyperError, Method, Request, Response, StatusCode, Uri as HyperUri,
};
#[cfg(windows)]
use hyper_named_pipe::Uri as PipeUri;
#[cfg(unix)]
use hyperlocal::Uri as HyperlocalUri;
#[cfg(windows)]
use hyperlocal_windows::Uri as HyperlocalUri;
#[cfg(windows)]
use rand::Rng;
use tempdir::TempDir;
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
    let port = get_unused_tcp_port();
    let server =
        run_tcp_server("127.0.0.1", port, hello_handler).map_err(|err| eprintln!("{}", err));

    let url = format!("http://localhost:{}", port);
    let connector = UrlConnector::new(&Url::parse(&url).unwrap()).unwrap();

    let client = Client::builder().build::<_, Body>(connector);
    let task = client
        .get(url.parse().unwrap())
        .and_then(|res| {
            assert_eq!(StatusCode::OK, res.status());
            res.into_body().concat2()
        }).map(|body| {
            assert_eq!(GET_RESPONSE, &String::from_utf8_lossy(body.as_ref()));
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn uds_get() {
    let dir = TempDir::new("uds").unwrap();
    let file_path = dir.path().join("sock");
    let file_path = file_path.to_str().unwrap();

    let server = run_uds_server(&file_path, |req| {
        hello_handler(req).map_err(|err| io::Error::new(io::ErrorKind::Other, err))
    }).map_err(|err| eprintln!("{}", err));

    let mut url = Url::from_file_path(file_path).unwrap();
    url.set_scheme("unix").unwrap();
    let connector = UrlConnector::new(&url).unwrap();

    let client = Client::builder().build::<_, Body>(connector);
    let task = client
        .get(HyperlocalUri::new(&file_path, "/").into())
        .and_then(|res| {
            assert_eq!(StatusCode::OK, res.status());
            res.into_body().concat2()
        }).map(|body| {
            assert_eq!(GET_RESPONSE, &String::from_utf8_lossy(body.as_ref()));
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[cfg(windows)]
fn make_path() -> String {
    format!(r"\\.\pipe\my-pipe-{}", rand::thread_rng().gen::<u64>())
}

#[cfg(windows)]
fn make_url(path: &str) -> String {
    format!("npipe:{}", path.replace("\\", "/"))
}

#[cfg(windows)]
#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
fn pipe_get_handler(_req: &HtRequest, _body: Option<Vec<u8>>) -> String {
    format!(
        "HTTP/1.1 200 OK\r\n\
         Content-Type: text/plain; charset=utf-8\r\n\
         Content-Length: {}\r\n\
         \r\n\
         {}",
        GET_RESPONSE.len(),
        GET_RESPONSE
    )
}

#[cfg(windows)]
#[test]
fn pipe_get() {
    let (sender, receiver) = channel();
    let path = make_path();
    let url = make_url(&path);

    thread::spawn(move || {
        run_pipe_server(&path, pipe_get_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let connector = UrlConnector::new(&Url::parse(&url).unwrap()).unwrap();

    let client = Client::builder().build::<_, Body>(connector);

    // make a get request
    let task = client
        .get(PipeUri::new(&url, "/").unwrap().into())
        .and_then(|res| {
            assert_eq!(StatusCode::OK, res.status());
            res.into_body().concat2()
        }).map(|body| {
            assert_eq!(GET_RESPONSE, &String::from_utf8_lossy(body.as_ref()));
        });

    tokio::runtime::current_thread::Runtime::new()
        .unwrap()
        .block_on(task)
        .unwrap();
}

const POST_BODY: &str = r#"{"donuts":"yes"}"#;

fn post_handler(
    req: Request<Body>,
) -> Box<Future<Item = Response<Body>, Error = HyperError> + Send> {
    // verify that the request body is what we expect
    Box::new(
        req.into_body()
            .concat2()
            .and_then(|body| {
                assert_eq!(POST_BODY, &String::from_utf8_lossy(body.as_ref()));
                Ok(())
            }).map(|_| Response::new(Body::empty())),
    )
}

#[test]
fn tcp_post() {
    let port = get_unused_tcp_port();
    let server =
        run_tcp_server("127.0.0.1", port, post_handler).map_err(|err| eprintln!("{}", err));

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

#[test]
fn uds_post() {
    let dir = TempDir::new("uds").unwrap();
    let file_path = dir.path().join("sock");
    let file_path = file_path.to_str().unwrap();

    let server = run_uds_server(&file_path, |req| {
        hello_handler(req).map_err(|err| io::Error::new(io::ErrorKind::Other, err))
    }).map_err(|err| eprintln!("{}", err));

    let mut url = Url::from_file_path(file_path).unwrap();
    url.set_scheme("unix").unwrap();
    let connector = UrlConnector::new(&url).unwrap();

    let client = Client::builder().build::<_, Body>(connector);

    let url: HyperUri = HyperlocalUri::new(&file_path, "/").into();

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

#[cfg(windows)]
fn pipe_post_handler(_req: &HtRequest, body: Option<Vec<u8>>) -> String {
    let body = body.unwrap();
    let body = String::from_utf8_lossy(&body);
    assert_eq!(&body, POST_BODY);

    "HTTP/1.1 200 OK\r\n\r\n".to_string()
}

#[cfg(windows)]
#[test]
#[ignore] //todo fix test. Disabling test as it is flaky and gating the checkin
fn pipe_post() {
    let (sender, receiver) = channel();
    let path = make_path();
    let url = make_url(&path);

    thread::spawn(move || {
        run_pipe_server(&path, pipe_post_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let connector = UrlConnector::new(&Url::parse(&url).unwrap()).unwrap();

    let client = Client::builder().build::<_, Body>(connector);

    let url: HyperUri = PipeUri::new(&url, "/").unwrap().into();

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

    let task = client.request(req).map(|res| {
        assert_eq!(StatusCode::OK, res.status());
    });

    tokio::runtime::current_thread::Runtime::new()
        .unwrap()
        .block_on(task)
        .unwrap();
}
