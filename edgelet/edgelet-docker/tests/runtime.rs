// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate base64;
extern crate futures;
extern crate hyper;
#[macro_use]
extern crate serde_json;
extern crate tokio_core;
extern crate url;

extern crate docker;
extern crate edgelet_core;
extern crate edgelet_docker;
extern crate edgelet_test_utils;

use std::collections::HashMap;
use std::str;
use std::sync::mpsc::channel;
use std::thread;

use futures::future;
use futures::prelude::*;
use hyper::header::{ContentLength, ContentType};
use hyper::server::{Request, Response};
use hyper::{Error as HyperError, Method, StatusCode};
use tokio_core::reactor::Core;
use url::Url;
use url::form_urlencoded::parse as parse_query;

#[cfg(unix)]
use docker::models::AuthConfig;
use docker::models::{ContainerCreateBody, ContainerHostConfig, ContainerNetworkSettings,
                     ContainerSummary, HostConfig, HostConfigPortBindings, ImageDeleteResponseItem};
use edgelet_core::{Module, ModuleRegistry, ModuleRuntime, ModuleSpec};
use edgelet_docker::{DockerConfig, DockerModuleRuntime};
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
        run_tcp_server("127.0.0.1", port, image_pull_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    let auth = AuthConfig::new()
        .with_username("u1".to_string())
        .with_password("bleh".to_string())
        .with_email("u1@bleh.com".to_string())
        .with_serveraddress("svr1".to_string());
    let config = DockerConfig::new(IMAGE_NAME, ContainerCreateBody::new(), Some(auth)).unwrap();

    let task = mri.pull(&config);
    core.run(task).unwrap();
}

#[cfg(unix)]
#[cfg_attr(feature = "cargo-clippy", allow(needless_pass_by_value))]
fn image_pull_with_creds_handler(req: Request) -> Box<Future<Item = Response, Error = HyperError>> {
    // verify that path is /images/create and that the "fromImage" query
    // parameter has the image name we expect
    assert_eq!(req.path(), "/images/create");

    let query_map: HashMap<String, String> = parse_query(req.query().unwrap().as_bytes())
        .into_owned()
        .collect();
    assert!(query_map.contains_key("fromImage"));
    assert_eq!(query_map.get("fromImage"), Some(&IMAGE_NAME.to_string()));

    // verify registry creds
    let auth_str = req.headers()
        .get_raw("X-Registry-Auth")
        .unwrap()
        .iter()
        .map(|bytes| base64::decode(bytes).unwrap())
        .map(|raw| str::from_utf8(&raw).unwrap().to_owned())
        .collect::<Vec<String>>()
        .join("");
    let auth_config: AuthConfig = serde_json::from_str(&auth_str.to_string()).unwrap();
    assert_eq!(auth_config.username(), Some(&"u1".to_string()));
    assert_eq!(auth_config.password(), Some(&"bleh".to_string()));
    assert_eq!(auth_config.email(), Some(&"u1@bleh.com".to_string()));
    assert_eq!(auth_config.serveraddress(), Some(&"svr1".to_string()));

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
fn image_pull_with_creds_succeeds() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, image_pull_with_creds_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    let auth = AuthConfig::new()
        .with_username("u1".to_string())
        .with_password("bleh".to_string())
        .with_email("u1@bleh.com".to_string())
        .with_serveraddress("svr1".to_string());
    let config = DockerConfig::new(IMAGE_NAME, ContainerCreateBody::new(), Some(auth)).unwrap();

    let task = mri.pull(&config);
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
        run_tcp_server("127.0.0.1", port, image_remove_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mut mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    let task = ModuleRegistry::remove(&mut mri, IMAGE_NAME);
    core.run(task).unwrap();
}

fn container_create_handler(req: Request) -> Box<Future<Item = Response, Error = HyperError>> {
    assert_eq!(req.method(), &Method::Post);
    assert_eq!(req.path(), "/containers/create");

    let response = json!({
        "Id": "12345",
        "Warnings": []
    }).to_string();

    Box::new(
        req.body()
            .concat2()
            .and_then(|body| {
                let create_options: ContainerCreateBody =
                    serde_json::from_slice(body.as_ref()).unwrap();

                assert_eq!("nginx:latest", create_options.image().unwrap());

                for v in vec!["k1=v1", "k2=v2", "k3=v3", "k4=v4", "k5=v5"].iter() {
                    assert!(create_options.env().unwrap().contains(&v.to_string()))
                }

                let port_bindings = create_options
                    .host_config()
                    .unwrap()
                    .port_bindings()
                    .unwrap();
                assert_eq!(
                    "8080",
                    port_bindings
                        .get("80/tcp")
                        .unwrap()
                        .iter()
                        .next()
                        .unwrap()
                        .host_port()
                        .unwrap()
                );
                assert_eq!(
                    "11022",
                    port_bindings
                        .get("22/tcp")
                        .unwrap()
                        .iter()
                        .next()
                        .unwrap()
                        .host_port()
                        .unwrap()
                );

                assert!(
                    create_options
                        .networking_config()
                        .unwrap()
                        .endpoints_config()
                        .unwrap()
                        .contains_key(&"edge-network".to_string())
                );

                Ok(())
            })
            .map(|_| {
                Response::new()
                    .with_header(ContentLength(response.len() as u64))
                    .with_header(ContentType::json())
                    .with_body(response)
                    .with_status(StatusCode::Ok)
            }),
    )
}

#[test]
fn container_create_succeeds() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, container_create_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut env = HashMap::new();
    env.insert("k1".to_string(), "v1".to_string());
    env.insert("k2".to_string(), "v2".to_string());
    env.insert("k3".to_string(), "v3".to_string());

    // add some create options
    let mut port_bindings = HashMap::new();
    port_bindings.insert(
        "22/tcp".to_string(),
        vec![
            HostConfigPortBindings::new().with_host_port("11022".to_string()),
        ],
    );
    port_bindings.insert(
        "80/tcp".to_string(),
        vec![
            HostConfigPortBindings::new().with_host_port("8080".to_string()),
        ],
    );

    let create_options = ContainerCreateBody::new()
        .with_host_config(HostConfig::new().with_port_bindings(port_bindings))
        .with_env(vec!["k4=v4".to_string(), "k5=v5".to_string()]);

    let module_config = ModuleSpec::new(
        "m1",
        "docker",
        DockerConfig::new("nginx:latest", create_options, None).unwrap(),
        env,
    ).unwrap();

    let mut core = Core::new().unwrap();
    let mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap()
        .with_network_id("edge-network".to_string());

    let task = mri.create(module_config);
    core.run(task).unwrap();
}

fn container_start_handler(req: Request) -> Box<Future<Item = Response, Error = HyperError>> {
    assert_eq!(req.method(), &Method::Post);
    assert_eq!(req.path(), "/containers/m1/start");

    Box::new(future::ok(Response::new().with_status(StatusCode::Ok)))
}

#[test]
fn container_start_succeeds() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, container_start_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    let task = mri.start("m1");
    core.run(task).unwrap();
}

fn container_stop_handler(req: Request) -> Box<Future<Item = Response, Error = HyperError>> {
    assert_eq!(req.method(), &Method::Post);
    assert_eq!(req.path(), "/containers/m1/stop");

    Box::new(future::ok(Response::new().with_status(StatusCode::Ok)))
}

#[test]
fn container_stop_succeeds() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, container_stop_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    let task = mri.stop("m1");
    core.run(task).unwrap();
}

fn container_remove_handler(req: Request) -> Box<Future<Item = Response, Error = HyperError>> {
    assert_eq!(req.method(), &Method::Delete);
    assert_eq!(req.path(), "/containers/m1");

    Box::new(future::ok(Response::new().with_status(StatusCode::Ok)))
}

#[test]
fn container_remove_succeeds() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, container_remove_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mut mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    let task = ModuleRuntime::remove(&mut mri, "m1");
    core.run(task).unwrap();
}

fn container_list_handler(req: Request) -> Box<Future<Item = Response, Error = HyperError>> {
    assert_eq!(req.method(), &Method::Get);
    assert_eq!(req.path(), "/containers/json");

    let query_map: HashMap<String, String> = parse_query(req.query().unwrap().as_bytes())
        .into_owned()
        .collect();
    assert!(query_map.contains_key("filters"));
    assert_eq!(
        query_map.get("filters"),
        Some(&json!({
            "label":
                vec![
                    "net.azure-devices.edge.owner=Microsoft.Azure.Devices.Edge.Agent",
                ]
        }).to_string())
    );

    let mut labels = HashMap::new();
    labels.insert("l1".to_string(), "v1".to_string());
    labels.insert("l2".to_string(), "v2".to_string());
    labels.insert("l3".to_string(), "v3".to_string());

    let modules = vec![
        ContainerSummary::new(
            "m1".to_string(),
            vec!["/m1".to_string()],
            "nginx:latest".to_string(),
            "img1".to_string(),
            "".to_string(),
            10,
            vec![],
            10,
            10,
            labels.clone(),
            "".to_string(),
            "".to_string(),
            ContainerHostConfig::new(""),
            ContainerNetworkSettings::new(HashMap::new()),
            vec![],
        ),
        ContainerSummary::new(
            "m2".to_string(),
            vec!["/m2".to_string()],
            "ubuntu:latest".to_string(),
            "img2".to_string(),
            "".to_string(),
            10,
            vec![],
            10,
            10,
            labels.clone(),
            "".to_string(),
            "".to_string(),
            ContainerHostConfig::new(""),
            ContainerNetworkSettings::new(HashMap::new()),
            vec![],
        ),
        ContainerSummary::new(
            "m3".to_string(),
            vec!["/m3".to_string()],
            "mongo:latest".to_string(),
            "img3".to_string(),
            "".to_string(),
            10,
            vec![],
            10,
            10,
            labels.clone(),
            "".to_string(),
            "".to_string(),
            ContainerHostConfig::new(""),
            ContainerNetworkSettings::new(HashMap::new()),
            vec![],
        ),
    ];

    let response = serde_json::to_string(&modules).unwrap();
    Box::new(future::ok(
        Response::new()
            .with_header(ContentLength(response.len() as u64))
            .with_header(ContentType::json())
            .with_body(response)
            .with_status(StatusCode::Ok),
    ))
}

#[test]
fn container_list_succeeds() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, container_list_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    let task = mri.list();
    let modules = core.run(task).unwrap();

    assert_eq!(3, modules.len());

    assert_eq!("m1", modules[0].name());
    assert_eq!("m2", modules[1].name());
    assert_eq!("m3", modules[2].name());

    assert_eq!("img1", modules[0].config().image_id().unwrap().as_str());
    assert_eq!("img2", modules[1].config().image_id().unwrap().as_str());
    assert_eq!("img3", modules[2].config().image_id().unwrap().as_str());

    assert_eq!("nginx:latest", modules[0].config().image());
    assert_eq!("ubuntu:latest", modules[1].config().image());
    assert_eq!("mongo:latest", modules[2].config().image());

    for i in 0..3 {
        for j in 0..3 {
            assert_eq!(
                modules[i]
                    .config()
                    .create_options()
                    .labels()
                    .unwrap()
                    .get(&format!("l{}", j + 1)),
                Some(&format!("v{}", j + 1))
            );
        }
    }
}
