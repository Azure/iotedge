// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

extern crate edgelet_http;
extern crate edgelet_test_utils;
extern crate futures;
extern crate hyper;
#[cfg(windows)]
extern crate hyper_named_pipe;
#[cfg(unix)]
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

use edgelet_http::UrlConnector;
#[cfg(windows)]
use edgelet_test_utils::run_pipe_server;
use edgelet_test_utils::run_uds_server;
use edgelet_test_utils::{get_unused_tcp_port, run_tcp_server};
use futures::future;
use futures::prelude::*;
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
        })
        .map(|body| {
            assert_eq!(GET_RESPONSE, &String::from_utf8_lossy(body.as_ref()));
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
#[cfg_attr(windows, ignore)] // TODO: remove when windows build servers are upgraded to RS5
fn uds_get() {
    let dir = TempDir::new("uds").unwrap();
    let file_path = dir.path().join("sock");
    let file_path = file_path.to_str().unwrap();

    let server = run_uds_server(&file_path, |req| {
        hello_handler(req).map_err(|err| io::Error::new(io::ErrorKind::Other, err))
    })
    .map_err(|err| eprintln!("{}", err));

    let mut url = Url::from_file_path(file_path).unwrap();
    url.set_scheme("unix").unwrap();
    let connector = UrlConnector::new(&url).unwrap();

    let client = Client::builder().build::<_, Body>(connector);
    let task = client
        .get(HyperlocalUri::new(&file_path, "/").into())
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

#[cfg(windows)]
fn make_path() -> String {
    format!(r"\\.\pipe\my-pipe-{}", rand::thread_rng().gen::<u64>())
}

#[cfg(windows)]
fn make_url(path: &str) -> String {
    format!("npipe:{}", path.replace("\\", "/"))
}

#[cfg(windows)]
#[allow(clippy::needless_pass_by_value)]
fn pipe_get_handler(_req: Request<Body>) -> impl Future<Item = Response<Body>, Error = io::Error> {
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

#[cfg(windows)]
#[test]
fn pipe_get() {
    let path = make_path();
    let url = make_url(&path);

    let server = run_pipe_server(path.into(), pipe_get_handler).map_err(|err| eprintln!("{}", err));

    let connector = UrlConnector::new(&Url::parse(&url).unwrap()).unwrap();

    let client = Client::builder().build::<_, Body>(connector);

    // make a get request
    let task = client
        .get(PipeUri::new(&url, "/").unwrap().into())
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
) -> Box<Future<Item = Response<Body>, Error = HyperError> + Send> {
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
#[cfg_attr(windows, ignore)] // TODO: remove when windows build servers are upgraded to RS5
fn uds_post() {
    let dir = TempDir::new("uds").unwrap();
    let file_path = dir.path().join("sock");
    let file_path = file_path.to_str().unwrap();

    let server = run_uds_server(&file_path, |req| {
        hello_handler(req).map_err(|err| io::Error::new(io::ErrorKind::Other, err))
    })
    .map_err(|err| eprintln!("{}", err));

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
fn pipe_post_handler(req: Request<Body>) -> impl Future<Item = Response<Body>, Error = io::Error> {
    req.into_body().concat2().then(|body| {
        let body = body.expect("couldn't read request body");
        let body = String::from_utf8_lossy(&body);
        assert_eq!(&body, POST_BODY);
        Ok(Response::new(Body::default()))
    })
}

#[cfg(windows)]
#[test]
fn pipe_post() {
    let path = make_path();
    let url = make_url(&path);

    let server =
        run_pipe_server(path.into(), pipe_post_handler).map_err(|err| eprintln!("{}", err));

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

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}
