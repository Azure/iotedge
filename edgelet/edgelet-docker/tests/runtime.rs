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
use std::sync::{Arc, RwLock};
use std::thread;
use std::time::Duration;

use futures::prelude::*;
use futures::{future, Stream};
use hyper::header::{ContentLength, ContentType};
use hyper::server::{Request, Response};
use hyper::{Error as HyperError, Method, StatusCode};
use tokio_core::reactor::Core;
use url::form_urlencoded::parse as parse_query;
use url::Url;

#[cfg(unix)]
use docker::models::AuthConfig;
use docker::models::{
    ContainerCreateBody, ContainerHostConfig, ContainerNetworkSettings, ContainerSummary,
    HostConfig, HostConfigPortBindings, ImageDeleteResponseItem,
};
use edgelet_core::{LogOptions, LogTail, Module, ModuleRegistry, ModuleRuntime, ModuleSpec};
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

                println!("Create options {:?}", create_options.clone());

                assert_eq!("nginx:latest", create_options.image().unwrap());

                for v in vec!["/do/the/custom/command", "with these args"].iter() {
                    assert!(create_options.cmd().unwrap().contains(&v.to_string()));
                }

                for v in vec![
                    "/also/do/the/entrypoint".to_string(),
                    "and this".to_string(),
                ].iter()
                {
                    assert!(
                        create_options
                            .entrypoint()
                            .unwrap()
                            .contains(&v.to_string())
                    );
                }

                for v in vec!["k1=v1", "k2=v2", "k3=v3", "k4=v4", "k5=v5"].iter() {
                    assert!(create_options.env().unwrap().contains(&v.to_string()));
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

                let volumes = create_options.volumes().unwrap();
                println!("Volumes: {:?}", volumes);
                assert_eq!(*volumes, json!({"/test1": {}, "/test2": {}}));

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
        vec![HostConfigPortBindings::new().with_host_port("11022".to_string())],
    );
    port_bindings.insert(
        "80/tcp".to_string(),
        vec![HostConfigPortBindings::new().with_host_port("8080".to_string())],
    );
    let memory: i64 = 3221225472;
    let volumes = json!({"/test1": {}, "/test2": {}});
    let create_options = ContainerCreateBody::new()
        .with_host_config(
            HostConfig::new()
                .with_port_bindings(port_bindings)
                .with_memory(memory)
                .with_binds(vec!["/a:/a".to_string()]),
        )
        .with_cmd(vec![
            "/do/the/custom/command".to_string(),
            "with these args".to_string(),
        ])
        .with_entrypoint(vec![
            "/also/do/the/entrypoint".to_string(),
            "and this".to_string(),
        ])
        .with_env(vec!["k4=v4".to_string(), "k5=v5".to_string()])
        .with_volumes(volumes);

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

    let task = mri.stop("m1", None);
    core.run(task).unwrap();
}

fn container_stop_with_timeout_handler(
    req: Request,
) -> Box<Future<Item = Response, Error = HyperError>> {
    assert_eq!(req.method(), &Method::Post);
    assert_eq!(req.path(), "/containers/m1/stop");
    assert_eq!(req.query().unwrap(), "t=600");

    Box::new(future::ok(Response::new().with_status(StatusCode::Ok)))
}

#[test]
fn container_stop_with_timeout_succeeds() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server(
            "127.0.0.1",
            port,
            container_stop_with_timeout_handler,
            &sender,
        );
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    let task = mri.stop("m1", Some(Duration::from_secs(600)));
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
            "label": vec!["net.azure-devices.edge.owner=Microsoft.Azure.Devices.Edge.Agent"]
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

fn container_logs_handler(req: Request) -> Box<Future<Item = Response, Error = HyperError>> {
    assert_eq!(req.method(), &Method::Get);
    assert_eq!(req.path(), "/containers/mod1/logs");

    let query_map: HashMap<String, String> = parse_query(req.query().unwrap().as_bytes())
        .into_owned()
        .collect();
    assert!(query_map.contains_key("stdout"));
    assert!(query_map.contains_key("stderr"));
    assert!(query_map.contains_key("follow"));
    assert!(query_map.contains_key("tail"));
    assert_eq!("true", query_map["follow"]);
    assert_eq!("all", query_map["tail"]);

    let body = vec![
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0d, 0x52, 0x6f, 0x73, 0x65, 0x73, 0x20, 0x61,
        0x72, 0x65, 0x20, 0x72, 0x65, 0x64, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x76,
        0x69, 0x6f, 0x6c, 0x65, 0x74, 0x73, 0x20, 0x61, 0x72, 0x65, 0x20, 0x62, 0x6c, 0x75, 0x65,
    ];

    Box::new(future::ok(
        Response::new().with_body(body).with_status(StatusCode::Ok),
    ))
}

#[test]
fn container_logs_succeeds() {
    let (sender, receiver) = channel();

    let port = get_unused_tcp_port();
    thread::spawn(move || {
        run_tcp_server("127.0.0.1", port, container_logs_handler, &sender);
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    let options = LogOptions::new().with_follow(true).with_tail(LogTail::All);
    let task = mri.logs("mod1", &options);
    let logs = core.run(task).unwrap();

    let expected_body = [
        0x01u8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0d, 0x52, 0x6f, 0x73, 0x65, 0x73, 0x20, 0x61,
        0x72, 0x65, 0x20, 0x72, 0x65, 0x64, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x76,
        0x69, 0x6f, 0x6c, 0x65, 0x74, 0x73, 0x20, 0x61, 0x72, 0x65, 0x20, 0x62, 0x6c, 0x75, 0x65,
    ];

    let assert = logs.concat2().and_then(|b| {
        assert_eq!(&expected_body[..], b.as_ref());
        Ok(())
    });
    core.run(assert).unwrap();
}

#[test]
fn runtime_init_network_does_not_exist_create() {
    //arrange
    let (sender, receiver) = channel();

    let list_got_called_lock = Arc::new(RwLock::new(false));
    let list_got_called_lock_cloned = list_got_called_lock.clone();

    let create_got_called_lock = Arc::new(RwLock::new(false));
    let create_got_called_lock_cloned = create_got_called_lock.clone();

    let port = get_unused_tcp_port();

    //let mut got_called = false;

    thread::spawn(move || {
        run_tcp_server(
            "127.0.0.1",
            port,
            move |req: Request| {
                let method = req.method();
                match method {
                    &Method::Get => {
                        let mut list_got_called_w = list_got_called_lock.write().unwrap();
                        *list_got_called_w = true;

                        assert_eq!(req.path(), "/networks");

                        let response = json!([]).to_string();

                        return Box::new(future::ok(
                            Response::new()
                                .with_header(ContentLength(response.len() as u64))
                                .with_header(ContentType::json())
                                .with_body(response)
                                .with_status(StatusCode::Ok),
                        ));
                    }
                    &Method::Post => {
                        //Netowk create.
                        let mut create_got_called_w = create_got_called_lock.write().unwrap();
                        *create_got_called_w = true;

                        assert_eq!(req.path(), "/networks/create");

                        let response = json!({
                        "Id": "12345",
                        "Warnings": ""
                    }).to_string();

                        return Box::new(future::ok(
                            Response::new()
                                .with_header(ContentLength(response.len() as u64))
                                .with_header(ContentType::json())
                                .with_body(response)
                                .with_status(StatusCode::Ok),
                        ));
                    }
                    _ => panic!("Method is not a get neither a post."),
                }
            },
            &sender,
        );
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap()
        .with_network_id("azure-iot-edge".to_string());

    //act
    let task = mri.init();
    core.run(task).unwrap();

    //assert
    assert_eq!(true, *list_got_called_lock_cloned.read().unwrap());
    assert_eq!(true, *create_got_called_lock_cloned.read().unwrap());
}

#[test]
fn runtime_init_network_exist_do_not_create() {
    //arrange
    let (sender, receiver) = channel();

    let list_got_called_lock = Arc::new(RwLock::new(false));
    let list_got_called_lock_cloned = list_got_called_lock.clone();

    let create_got_called_lock = Arc::new(RwLock::new(false));
    let create_got_called_lock_cloned = create_got_called_lock.clone();

    let port = get_unused_tcp_port();

    //let mut got_called = false;

    thread::spawn(move || {
        run_tcp_server(
            "127.0.0.1",
            port,
            move |req: Request| {
                let method = req.method();
                match method {
                    &Method::Get => {
                        let mut list_got_called_w = list_got_called_lock.write().unwrap();
                        *list_got_called_w = true;

                        assert_eq!(req.path(), "/networks");

                        let response = json!(
                        [
                            {
                                "Name": "azure-iot-edge",
                                "Id": "8e3209d08ed5e73d1c9c8e7580ddad232b6dceb5bf0c6d74cadbed75422eef0e",
                                "Created": "0001-01-01T00:00:00Z",
                                "Scope": "local",
                                "Driver": "bridge",
                                "EnableIPv6": false,
                                "Internal": false,
                                "Attachable": false,
                                "Ingress": false,
                                "IPAM": {
                                "Driver": "bridge",
                                "Config": []
                                },
                                "Containers": {},
                                "Options": {}
                            }
                        ]
                    ).to_string();

                        return Box::new(future::ok(
                            Response::new()
                                .with_header(ContentLength(response.len() as u64))
                                .with_header(ContentType::json())
                                .with_body(response)
                                .with_status(StatusCode::Ok),
                        ));
                    }
                    &Method::Post => {
                        //Netowk create.
                        let mut create_got_called_w = create_got_called_lock.write().unwrap();
                        *create_got_called_w = true;

                        assert_eq!(req.path(), "/networks/create");

                        let response = json!({
                        "Id": "12345",
                        "Warnings": ""
                    }).to_string();

                        return Box::new(future::ok(
                            Response::new()
                                .with_header(ContentLength(response.len() as u64))
                                .with_header(ContentType::json())
                                .with_body(response)
                                .with_status(StatusCode::Ok),
                        ));
                    }
                    _ => panic!("Method is not a get neither a post."),
                }
            },
            &sender,
        );
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap()
        .with_network_id("azure-iot-edge".to_string());

    //act
    let task = mri.init();
    core.run(task).unwrap();

    //assert
    assert_eq!(true, *list_got_called_lock_cloned.read().unwrap());
    assert_eq!(false, *create_got_called_lock_cloned.read().unwrap());
}

#[test]
fn runtime_system_info_succeed() {
    //arrange
    let (sender, receiver) = channel();

    let system_info_got_called_lock = Arc::new(RwLock::new(false));
    let system_info_got_called_lock_cloned = system_info_got_called_lock.clone();

    let port = get_unused_tcp_port();

    thread::spawn(move || {
        run_tcp_server(
            "127.0.0.1",
            port,
            move |req: Request| {
                let method = req.method();
                match method {
                    &Method::Get => {
                        let mut system_info_got_called_w =
                            system_info_got_called_lock.write().unwrap();
                        *system_info_got_called_w = true;

                        assert_eq!(req.path(), "/info");

                        let response = json!(
                            {
                                "OSType": "linux",
                                "Architecture": "x86_64",
                            }
                    ).to_string();

                        return Box::new(future::ok(
                            Response::new()
                                .with_header(ContentLength(response.len() as u64))
                                .with_header(ContentType::json())
                                .with_body(response)
                                .with_status(StatusCode::Ok),
                        ));
                    }
                    _ => panic!("Method is not a get neither a post."),
                }
            },
            &sender,
        );
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    //act
    let task = mri.system_info();
    let system_info = core.run(task).unwrap();

    //assert
    assert_eq!(true, *system_info_got_called_lock_cloned.read().unwrap());
    assert_eq!("linux", system_info.os_type());
    assert_eq!("x86_64", system_info.architecture());
}

#[test]
fn runtime_system_info_none_returns_unkown() {
    //arrange
    let (sender, receiver) = channel();

    let system_info_got_called_lock = Arc::new(RwLock::new(false));
    let system_info_got_called_lock_cloned = system_info_got_called_lock.clone();

    let port = get_unused_tcp_port();

    thread::spawn(move || {
        run_tcp_server(
            "127.0.0.1",
            port,
            move |req: Request| {
                let method = req.method();
                match method {
                    &Method::Get => {
                        let mut system_info_got_called_w =
                            system_info_got_called_lock.write().unwrap();
                        *system_info_got_called_w = true;

                        assert_eq!(req.path(), "/info");

                        let response = json!({}).to_string();

                        return Box::new(future::ok(
                            Response::new()
                                .with_header(ContentLength(response.len() as u64))
                                .with_header(ContentType::json())
                                .with_body(response)
                                .with_status(StatusCode::Ok),
                        ));
                    }
                    _ => panic!("Method is not a get neither a post."),
                }
            },
            &sender,
        );
    });

    // wait for server to get ready
    receiver.recv().unwrap();

    let mut core = Core::new().unwrap();
    let mri = DockerModuleRuntime::new(
        &Url::parse(&format!("http://localhost:{}/", port)).unwrap(),
        &core.handle(),
    ).unwrap();

    //act
    let task = mri.system_info();
    let system_info = core.run(task).unwrap();

    //assert
    assert_eq!(true, *system_info_got_called_lock_cloned.read().unwrap());
    assert_eq!("Unknown", system_info.os_type());
    assert_eq!("Unknown", system_info.architecture());
}
