#![deny(warnings)]

extern crate futures;
extern crate hyper;
#[cfg(unix)]
extern crate hyperlocal;
extern crate serde_json;
extern crate tokio_core;
extern crate url;

extern crate docker_mri;
extern crate docker_rs;
extern crate edgelet_core;
extern crate edgelet_test_utils;

#[cfg(unix)]
use std::collections::HashMap;
use std::sync::mpsc::channel;
use std::thread;

use futures::future;
use futures::prelude::*;
use hyper::{Error as HyperError, Method, StatusCode};
use hyper::header::{ContentLength, ContentType};
use hyper::server::{Request, Response};
use tokio_core::reactor::Core;
#[cfg(unix)]
use url::form_urlencoded::parse as parse_query;
use url::Url;

use docker_rs::models::ImageDeleteResponseItem;
use docker_mri::DockerModuleRuntime;
use edgelet_core::ModuleRegistry;
use edgelet_test_utils::{get_unused_tcp_port, run_tcp_server};

const IMAGE_NAME: &str = "nginx:latest";

#[cfg(unix)]
#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
fn image_pull_handler(req: Request) -> Box<Future<Item = Response, Error = HyperError>> {
    // verify that path is /images/create and that the "fromImage" query
    // parameter has the image name we expect
    assert_eq!(req.path(), "/images/create");

    let query_map: HashMap<String, String> = parse_query(req.query().unwrap().as_bytes())
        .into_owned()
        .collect();
    assert!(query_map.contains_key("fromImage"));
    assert_eq!(query_map.get("fromImage"), Some(&IMAGE_NAME.to_string()));

    let response = r#"
    {
        "Id": "img1",
        "Warnings": []
    }
    "#;
    Box::new(future::ok(
        Response::new()
            .with_header(ContentLength(response.len() as u64))
            .with_header(ContentType::json())
            .with_body(response)
            .with_status(StatusCode::Ok),
    ))
}

// This test is super flaky on Windows for some reason. It keeps occassionally
// failing on Windows with error 10054 which means the server keeps dropping the
// socket for no reason apparently.
#[cfg(unix)]
#[test]
fn image_pull_succeeds() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, &image_pull_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mut mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    let task = mri.pull(IMAGE_NAME);
    core.run(task).unwrap();
}

#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
fn image_remove_handler(req: Request) -> Box<Future<Item = Response, Error = HyperError>> {
    assert_eq!(req.method(), &Method::Delete);
    assert_eq!(req.path(), &format!("/images/{}", IMAGE_NAME));

    let response = serde_json::to_string(&vec![
        ImageDeleteResponseItem::new().with_deleted(IMAGE_NAME.to_string()),
    ]).unwrap();

    Box::new(future::ok(
        Response::new()
            .with_header(ContentLength(response.len() as u64))
            .with_header(ContentType::json())
            .with_body(response)
            .with_status(StatusCode::Ok),
    ))
}

#[test]
fn image_remove_succeeds() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, &image_remove_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mut mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    let task = mri.remove(IMAGE_NAME);
    core.run(task).unwrap();
}
