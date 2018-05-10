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
extern crate tokio_core;
extern crate url;

use std::sync::mpsc::channel;
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
use hyper::Error as HyperError;
use hyper::header::{ContentLength, ContentType};
use hyper::server::{Request, Response};
use hyper::{Client, Method, Request as ClientRequest, StatusCode};
#[cfg(windows)]
use hyper_named_pipe::Uri as PipeUri;
#[cfg(unix)]
use hyperlocal::Uri as HyperlocalUri;
#[cfg(windows)]
use rand::Rng;
use tokio_core::reactor::Core;
use url::Url;

const GET_RESPONSE: &str = "Yo";

fn hello_handler(_: Request) -> Box<Future<Item = Response, Error = HyperError>> {
    Box::new(future::ok(
        Response::new()
            .with_header(ContentLength(GET_RESPONSE.len() as u64))
            .with_body(GET_RESPONSE),
    ))
}

#[test]
fn tcp_get() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, &hello_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let url = format!("http://localhost:{}", port);
    let connector = UrlConnector::new(&Url::parse(&url).unwrap(), &core.handle()).unwrap();

    let client = Client::configure()
        .connector(connector)
        .build(&core.handle());
    let task = client
        .get(url.parse().unwrap())
        .and_then(|res| {
            assert_eq!(StatusCode::Ok, res.status());
            res.body().concat2()
        })
        .map(|body| {
            assert_eq!(GET_RESPONSE, &String::from_utf8_lossy(body.as_ref()));
        });

    core.run(task).unwrap();
}

#[cfg(unix)]
#[test]
fn uds_get() {
    let (sender, receiver) = channel();
    let file_path = "/tmp/edgelet_test_uds_get.sock";

    // make sure file gets deleted when test is done
    defer! {{
        ::std::fs::remove_file(&file_path).unwrap_or(());
    }}

    let path_copy = file_path.to_string();
    thread::spawn(move || {
        run_uds_server(&path_copy, &hello_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let connector = UrlConnector::new(
        &Url::parse(&format!("unix://{}", file_path)).unwrap(),
        &core.handle(),
    ).unwrap();

    let client = Client::configure()
        .connector(connector)
        .build(&core.handle());
    let task = client
        .get(HyperlocalUri::new(&file_path, "/").into())
        .and_then(|res| {
            assert_eq!(StatusCode::Ok, res.status());
            res.body().concat2()
        })
        .map(|body| {
            assert_eq!(GET_RESPONSE, &String::from_utf8_lossy(body.as_ref()));
        });

    core.run(task).unwrap();
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
        run_pipe_server(&path, &pipe_get_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let connector = UrlConnector::new(&Url::parse(&url).unwrap(), &core.handle()).unwrap();

    let hyper_client = Client::configure()
        .connector(connector)
        .build(&core.handle());

    // make a get request
    let task = hyper_client
        .get(PipeUri::new(&url, "/").unwrap().into())
        .and_then(|res| {
            assert_eq!(StatusCode::Ok, res.status());
            res.body().concat2()
        })
        .map(|body| {
            assert_eq!(GET_RESPONSE, &String::from_utf8_lossy(body.as_ref()));
        });

    core.run(task).unwrap();
}

const POST_BODY: &str = r#"{"donuts":"yes"}"#;

fn post_handler(req: Request) -> Box<Future<Item = Response, Error = HyperError>> {
    // verify that the request body is what we expect
    Box::new(
        req.body()
            .concat2()
            .and_then(|body| {
                assert_eq!(POST_BODY, &String::from_utf8_lossy(body.as_ref()));
                Ok(())
            })
            .map(|_| Response::new().with_status(StatusCode::Ok)),
    )
}

#[test]
fn tcp_post() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, &post_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let url = format!("http://localhost:{}", port);
    let connector = UrlConnector::new(&Url::parse(&url).unwrap(), &core.handle()).unwrap();

    let client = Client::configure()
        .connector(connector)
        .build(&core.handle());

    let url = url.parse().unwrap();
    let mut req = ClientRequest::new(Method::Post, url);
    req.headers_mut().set(ContentType::json());
    req.headers_mut().set(ContentLength(POST_BODY.len() as u64));
    req.set_body(POST_BODY);

    let task = client.request(req).map(|res| {
        assert_eq!(StatusCode::Ok, res.status());
    });

    core.run(task).unwrap();
}

#[cfg(unix)]
#[test]
fn uds_post() {
    let (sender, receiver) = channel();
    let file_path = "/tmp/edgelet_test_uds_post.sock";

    // make sure file gets deleted when test is done
    defer! {{
        ::std::fs::remove_file(&file_path).unwrap_or(());
    }}

    let path_copy = file_path.to_string();
    thread::spawn(move || {
        run_uds_server(&path_copy, &hello_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let connector = UrlConnector::new(
        &Url::parse(&format!("unix://{}", file_path)).unwrap(),
        &core.handle(),
    ).unwrap();

    let client = Client::configure()
        .connector(connector)
        .build(&core.handle());

    let url = HyperlocalUri::new(&file_path, "/").into();
    let mut req = ClientRequest::new(Method::Post, url);
    req.headers_mut().set(ContentType::json());
    req.headers_mut().set(ContentLength(POST_BODY.len() as u64));
    req.set_body(POST_BODY);

    let task = client.request(req).map(|res| {
        assert_eq!(StatusCode::Ok, res.status());
    });

    core.run(task).unwrap();
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
        run_pipe_server(&path, &pipe_post_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let connector = UrlConnector::new(&Url::parse(&url).unwrap(), &core.handle()).unwrap();

    let hyper_client = Client::configure()
        .connector(connector)
        .build(&core.handle());

    // make a post request
    let mut req = Request::new(Method::Post, PipeUri::new(&url, "/").unwrap().into());
    req.headers_mut().set(ContentType::json());
    req.headers_mut().set(ContentLength(POST_BODY.len() as u64));
    req.set_body(POST_BODY);

    let task = hyper_client.request(req).map(|res| {
        assert_eq!(StatusCode::Ok, res.status());
    });

    core.run(task).unwrap();
}
