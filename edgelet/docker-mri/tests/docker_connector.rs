#![deny(warnings)]

extern crate futures;
extern crate hyper;
#[cfg(unix)]
extern crate hyperlocal;
#[cfg(unix)]
#[macro_use(defer)]
extern crate scopeguard;
extern crate tokio_core;
extern crate url;

extern crate docker_mri;
extern crate edgelet_test_utils;

use std::sync::mpsc::channel;
use std::thread;

use futures::future;
use futures::prelude::*;
use hyper::{Client, Method, Request as ClientRequest, StatusCode};
use hyper::Error as HyperError;
use hyper::header::{ContentLength, ContentType};
use hyper::server::{Request, Response};
#[cfg(unix)]
use hyperlocal::Uri as HyperlocalUri;
use tokio_core::reactor::Core;
use url::Url;

use docker_mri::docker_connector::DockerConnector;
use edgelet_test_utils::{get_unused_tcp_port, run_tcp_server};
#[cfg(unix)]
use edgelet_test_utils::run_uds_server;

const GET_RESPONSE: &str = "Yo";

fn hello_handler(_: Request) -> Box<Future<Item = Response, Error = HyperError>> {
    Box::new(future::ok(
        Response::new()
            .with_header(ContentLength(GET_RESPONSE.len() as u64))
            .with_body(GET_RESPONSE),
    ))
}

#[test]
fn http_tcp_read_succeeds() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, &hello_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let url = format!("http://localhost:{}", port);
    let connector = DockerConnector::new(&Url::parse(&url).unwrap(), &core.handle()).unwrap();

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
fn http_uds_read_succeeds() {
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
    let connector = DockerConnector::new(
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
fn http_tcp_post_succeeds() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, &post_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let url = format!("http://localhost:{}", port);
    let connector = DockerConnector::new(&Url::parse(&url).unwrap(), &core.handle()).unwrap();

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
fn http_uds_post_succeeds() {
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
    let connector = DockerConnector::new(
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
