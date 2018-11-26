// Copyright (c) Microsoft. All rights reserved.

#![cfg(windows)]
#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]

extern crate futures;
extern crate httparse;
extern crate hyper;
extern crate rand;
extern crate tokio;
extern crate typed_headers;

extern crate edgelet_test_utils;
extern crate hyper_named_pipe;

use std::sync::mpsc::channel;
use std::thread;

use futures::future::Future;
use futures::Stream;
use httparse::Request;
use hyper::{
    Body, Client as HyperClient, Method, Request as HyperRequest, StatusCode, Uri as HyperUri,
};
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

#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
fn get_handler(_req: &Request, _body: Option<Vec<u8>>) -> String {
    "HTTP/1.1 200 OK\r\n\r\n".to_string()
}

#[test]
fn get() {
    let (sender, receiver) = channel();
    let path = make_path();
    let url = make_url(&path);

    thread::spawn(move || {
        run_pipe_server(&path, get_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let hyper_client = HyperClient::builder().build::<_, Body>(PipeConnector);

    // make a get request
    let task = hyper_client.get(url.into());
    let response = tokio::runtime::current_thread::Runtime::new()
        .unwrap()
        .block_on(task)
        .unwrap();
    assert_eq!(response.status(), StatusCode::OK);
}

const GET_RESPONSE: &str = "The answer is 42";

#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
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
        run_pipe_server(&path, get_with_body_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let hyper_client = HyperClient::builder().build::<_, Body>(PipeConnector);

    // make a get request
    let task = hyper_client
        .get(url.into())
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
    let url: HyperUri = make_url(&path).into();

    thread::spawn(move || {
        run_pipe_server(&path, post_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let hyper_client = HyperClient::builder().build::<_, Body>(PipeConnector);

    // make a post request
    let mut req = HyperRequest::builder()
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

    tokio::runtime::current_thread::Runtime::new()
        .unwrap()
        .block_on(task)
        .unwrap();
}
