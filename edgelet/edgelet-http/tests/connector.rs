// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate edgelet_http;
extern crate edgelet_test_utils;
extern crate futures;
#[cfg(windows)]
extern crate httparse;
extern crate hyper;
#[cfg(windows)]
extern crate hyper_named_pipe;
#[cfg(unix)]
extern crate hyperlocal;
#[cfg(windows)]
extern crate rand;
#[cfg(unix)]
#[macro_use(defer)]
extern crate scopeguard;
extern crate tokio;
extern crate typed_headers;
extern crate url;

#[cfg(unix)]
use std::io;
#[cfg(windows)]
use std::sync::mpsc::channel;
#[cfg(windows)]
use std::thread;

use edgelet_http::UrlConnector;
#[cfg(windows)]
use edgelet_test_utils::run_pipe_server;
#[cfg(unix)]
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
use rand::Rng;
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

#[cfg(unix)]
#[test]
fn uds_get() {
    let file_path = "/tmp/edgelet_test_uds_get.sock";

    // make sure file gets deleted when test is done
    defer! {{
        ::std::fs::remove_file(&file_path).unwrap_or(());
    }}

    let path_copy = file_path.to_string();
    let server = run_uds_server(&path_copy, |req| {
        hello_handler(req).map_err(|err| io::Error::new(io::ErrorKind::Other, err))
    }).map_err(|err| eprintln!("{}", err));

    let connector =
        UrlConnector::new(&Url::parse(&format!("unix://{}", file_path)).unwrap()).unwrap();

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

#[cfg(unix)]
#[test]
fn uds_post() {
    let file_path = "/tmp/edgelet_test_uds_post.sock";

    // make sure file gets deleted when test is done
    defer! {{
        ::std::fs::remove_file(&file_path).unwrap_or(());
    }}

    let path_copy = file_path.to_string();
    let server = run_uds_server(&path_copy, |req| {
        hello_handler(req).map_err(|err| io::Error::new(io::ErrorKind::Other, err))
    }).map_err(|err| eprintln!("{}", err));

    let connector =
        UrlConnector::new(&Url::parse(&format!("unix://{}", file_path)).unwrap()).unwrap();

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
