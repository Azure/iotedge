// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]
#![cfg(windows)]

extern crate futures;
extern crate httparse;
extern crate hyper;
extern crate rand;
extern crate tokio_core;

extern crate edgelet_test_utils;
extern crate hyper_named_pipe;

use std::sync::mpsc::channel;
use std::thread;

use futures::Stream;
use futures::future::Future;
use httparse::Request;
use hyper::header::{ContentLength, ContentType};
use hyper::{Client as HyperClient, Method, Request as HyperRequest, StatusCode};
use rand::Rng;
use tokio_core::reactor::Core;

use edgelet_test_utils::run_pipe_server;
use hyper_named_pipe::{PipeConnector, Uri};

fn make_path() -> String {
    format!(r"\\.\pipe\my-pipe-{}", rand::thread_rng().gen::<u64>())
}

fn make_url(path: &str) -> Uri {
    Uri::new(&format!("npipe:{}", path.replace("\\", "/")), "/").unwrap()
}

fn get_handler(_req: &Request, _body: Option<Vec<u8>>) -> String {
    "HTTP/1.1 200 OK\r\n\r\n".to_string()
}

#[test]
fn get() {
    let (sender, receiver) = channel();
    let path = make_path();
    let url = make_url(&path);

    thread::spawn(move || {
        run_pipe_server(&path, &get_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let hyper_client = HyperClient::configure()
        .connector(PipeConnector::new(core.handle()))
        .build(&core.handle());

    // make a get request
    let task = hyper_client.get(url.into());
    let response = core.run(task).unwrap();
    assert_eq!(response.status(), StatusCode::Ok);
}

const GET_RESPONSE: &str = "The answer is 42";

fn get_with_body_handler(_req: &Request, _body: Option<Vec<u8>>) -> String {
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

#[test]
fn get_with_body() {
    let (sender, receiver) = channel();
    let path = make_path();
    let url = make_url(&path);

    thread::spawn(move || {
        run_pipe_server(&path, &get_with_body_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let hyper_client = HyperClient::configure()
        .connector(PipeConnector::new(core.handle()))
        .build(&core.handle());

    // make a get request
    let task = hyper_client
        .get(url.into())
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

fn post_handler(_req: &Request, body: Option<Vec<u8>>) -> String {
    let body = body.unwrap();
    let body = String::from_utf8_lossy(&body);
    assert_eq!(&body, POST_BODY);

    "HTTP/1.1 200 OK\r\n\r\n".to_string()
}

#[test]
fn post() {
    let (sender, receiver) = channel();
    let path = make_path();
    let url = make_url(&path);

    thread::spawn(move || {
        run_pipe_server(&path, &post_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let hyper_client = HyperClient::configure()
        .connector(PipeConnector::new(core.handle()))
        .build(&core.handle());

    // make a post request
    let mut req = HyperRequest::new(Method::Post, url.into());
    req.headers_mut().set(ContentType::json());
    req.headers_mut().set(ContentLength(POST_BODY.len() as u64));
    req.set_body(POST_BODY);

    let task = hyper_client.request(req).map(|res| {
        assert_eq!(StatusCode::Ok, res.status());
    });

    core.run(task).unwrap();
}
