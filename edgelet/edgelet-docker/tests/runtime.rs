// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::too_many_lines)]

use std::collections::HashMap;
use std::str;
use std::sync::{Arc, RwLock};
use std::time::Duration;

use config::{Config, File, FileFormat};
use failure::Fail;
use futures::future;
use futures::prelude::*;
use hyper::{Body, Method, Request, Response, StatusCode};
use json_patch::merge;
use maplit::btreemap;
use serde_json::{self, json, Value as JsonValue};
use typed_headers::{mime, ContentLength, ContentType, HeaderMapExt};
use url::form_urlencoded::parse as parse_query;

use docker::models::{
    AuthConfig, ContainerCreateBody, ContainerHostConfig, ContainerNetworkSettings,
    ContainerSummary, HostConfig, HostConfigPortBindings, ImageDeleteResponseItem, NetworkConfig,
};

use edgelet_core::{
    GetTrustBundle, ImagePullPolicy, LogOptions, LogTail, MakeModuleRuntime, Module,
    ModuleRegistry, ModuleRuntime, ModuleSpec, RegistryOperation, RuntimeOperation,
};
use edgelet_docker::{DockerConfig, DockerModuleRuntime, Settings};
use edgelet_docker::{Error, ErrorKind};
use edgelet_test_utils::crypto::TestHsm;
use edgelet_test_utils::web::{
    make_req_dispatcher, HttpMethod, RequestHandler, RequestPath, ResponseFuture,
};
use edgelet_test_utils::{routes, run_tcp_server};
use hyper::Error as HyperError;
use provisioning::{ProvisioningResult, ReprovisioningStatus};

const IMAGE_NAME: &str = "nginx:latest";

const INVALID_IMAGE_NAME: &str = "invalidname:latest";
const INVALID_IMAGE_HOST: &str = "invalidhost.com/nginx:latest";

fn make_settings(merge_json: Option<JsonValue>) -> Settings {
    let mut config = Config::default();
    let mut config_json = json!({
        "provisioning": {
            "source": "manual",
            "device_connection_string": "HostName=moo.azure-devices.net;DeviceId=boo;SharedAccessKey=boo"
        },
        "agent": {
            "name": "edgeAgent",
            "type": "docker",
            "env": {},
            "config": {
                "image": "mcr.microsoft.com/azureiotedge-agent:1.0",
                "auth": {}
            }
        },
        "hostname": "zoo",
        "connect": {
            "management_uri": "unix:///var/run/iotedge/mgmt.sock",
            "workload_uri": "unix:///var/run/iotedge/workload.sock"
        },
        "listen": {
            "management_uri": "unix:///var/run/iotedge/mgmt.sock",
            "workload_uri": "unix:///var/run/iotedge/workload.sock"
        },
        "homedir": "/var/lib/iotedge",
        "moby_runtime": {
            "uri": "unix:///var/run/docker.sock",
            "network": "azure-iot-edge"
        }
    });

    if let Some(merge_json) = merge_json {
        merge(&mut config_json, &merge_json);
    }

    config
        .merge(File::from_str(&config_json.to_string(), FileFormat::Json))
        .unwrap();

    config.try_into().unwrap()
}

fn provisioning_result() -> ProvisioningResult {
    ProvisioningResult::new(
        "d1",
        "h1",
        None,
        ReprovisioningStatus::DeviceDataNotUpdated,
        None,
    )
}

fn crypto() -> impl GetTrustBundle {
    TestHsm::default()
}

fn make_get_networks_handler(
    on_get: impl Fn() -> String + Clone + Send + 'static,
) -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
    move |_| {
        let response = on_get();
        let response_len = response.len();

        let mut response = Response::new(response.into());
        response
            .headers_mut()
            .typed_insert(&ContentLength(response_len as u64));
        response
            .headers_mut()
            .typed_insert(&ContentType(mime::APPLICATION_JSON));
        Box::new(future::ok(response)) as ResponseFuture
    }
}

fn make_create_network_handler(
    on_post: impl Fn(Request<Body>) -> () + Clone + Send + 'static,
) -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
    move |req| {
        on_post(req);

        let response = json!({
            "Id": "12345",
            "Warnings": ""
        })
        .to_string();
        let response_len = response.len();

        let mut response = Response::new(response.into());
        response
            .headers_mut()
            .typed_insert(&ContentLength(response_len as u64));
        response
            .headers_mut()
            .typed_insert(&ContentType(mime::APPLICATION_JSON));
        Box::new(future::ok(response)) as ResponseFuture
    }
}

fn not_found_handler(_: Request<Body>) -> ResponseFuture {
    let response = Response::builder()
        .status(StatusCode::NOT_FOUND)
        .body(Body::default())
        .unwrap();

    Box::new(future::ok(response))
}

fn make_network_handler(
    on_get: impl Fn() -> String + Clone + Send + 'static,
    on_post: impl Fn(Request<Body>) -> () + Clone + Send + 'static,
) -> impl Fn(Request<Body>) -> Box<dyn Future<Item = Response<Body>, Error = HyperError> + Send> + Clone
{
    let dispatch_table = routes!(
        GET "/networks" => make_get_networks_handler(on_get),
        POST "/networks/create" => make_create_network_handler(on_post),
    );

    make_req_dispatcher(dispatch_table, Box::new(not_found_handler))
}

fn default_get_networks_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
    make_get_networks_handler(|| json!([]).to_string())
}

fn default_create_network_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
    make_create_network_handler(|_| ())
}

fn default_network_handler(
) -> impl Fn(Request<Body>) -> Box<dyn Future<Item = Response<Body>, Error = HyperError> + Send> + Clone
{
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
    );

    make_req_dispatcher(dispatch_table, Box::new(not_found_handler))
}

#[allow(clippy::needless_pass_by_value)]
fn invalid_image_name_pull_handler(req: Request<Body>) -> ResponseFuture {
    // verify that path is /images/create and that the "fromImage" query
    // parameter has the image name we expect
    assert_eq!(req.uri().path(), "/images/create");

    let query_map: HashMap<String, String> = parse_query(req.uri().query().unwrap().as_bytes())
        .into_owned()
        .collect();
    assert!(query_map.contains_key("fromImage"));
    assert_eq!(
        query_map.get("fromImage").map(AsRef::as_ref),
        Some(INVALID_IMAGE_NAME)
    );

    let response = format!(
        r#"{{
        "message": "manifest for {} not found"
    }}
    "#,
        INVALID_IMAGE_NAME
    );

    let response_len = response.len();

    let mut response = Response::new(response.into());
    response
        .headers_mut()
        .typed_insert(&ContentLength(response_len as u64));
    response
        .headers_mut()
        .typed_insert(&ContentType(mime::APPLICATION_JSON));
    *response.status_mut() = hyper::StatusCode::NOT_FOUND;

    Box::new(future::ok(response))
}

#[test]
fn image_pull_with_invalid_image_name_fails() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        POST "/images/create" => invalid_image_name_pull_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| {
            let auth = AuthConfig::new()
                .with_username("u1".to_string())
                .with_password("bleh".to_string())
                .with_email("u1@bleh.com".to_string())
                .with_serveraddress("svr1".to_string());
            let config = DockerConfig::new(
                INVALID_IMAGE_NAME.to_string(),
                ContainerCreateBody::new(),
                Some(auth),
            )
            .unwrap();

            runtime.pull(&config)
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);

    // Assert
    let err = runtime
        .block_on(task)
        .expect_err("Expected runtime pull method to fail due to invalid image name.");

    match (err.kind(), err.cause().and_then(Fail::downcast_ref)) {
        (
            edgelet_docker::ErrorKind::RegistryOperation(
                edgelet_core::RegistryOperation::PullImage(name),
            ),
            Some(edgelet_docker::ErrorKind::NotFound(message)),
        ) if name == INVALID_IMAGE_NAME => {
            assert_eq!(
                &format!("manifest for {} not found", INVALID_IMAGE_NAME),
                message
            );
        }

        _ => panic!(
            "Specific docker runtime message is expected for invalid image name. Got {:?}",
            err.kind()
        ),
    }
}

#[allow(clippy::needless_pass_by_value)]
fn invalid_image_host_pull_handler(req: Request<Body>) -> ResponseFuture {
    // verify that path is /images/create and that the "fromImage" query
    // parameter has the image name we expect
    assert_eq!(req.uri().path(), "/images/create");

    let query_map: HashMap<String, String> = parse_query(req.uri().query().unwrap().as_bytes())
        .into_owned()
        .collect();
    assert!(query_map.contains_key("fromImage"));
    assert_eq!(
        query_map.get("fromImage").map(AsRef::as_ref),
        Some(INVALID_IMAGE_HOST)
    );

    let response = format!(
        r#"
    {{
        "message":"Get https://invalidhost.com: dial tcp: lookup {} on X.X.X.X: no such host"
    }}
    "#,
        INVALID_IMAGE_HOST
    );
    let response_len = response.len();

    let mut response = Response::new(response.into());
    response
        .headers_mut()
        .typed_insert(&ContentLength(response_len as u64));
    response
        .headers_mut()
        .typed_insert(&ContentType(mime::APPLICATION_JSON));
    *response.status_mut() = hyper::StatusCode::INTERNAL_SERVER_ERROR;
    Box::new(future::ok(response))
}

#[test]
fn image_pull_with_invalid_image_host_fails() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        POST "/images/create" => invalid_image_host_pull_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| {
            let auth = AuthConfig::new()
                .with_username("u1".to_string())
                .with_password("bleh".to_string())
                .with_email("u1@bleh.com".to_string())
                .with_serveraddress("svr1".to_string());
            let config = DockerConfig::new(
                INVALID_IMAGE_HOST.to_string(),
                ContainerCreateBody::new(),
                Some(auth),
            )
            .unwrap();

            runtime.pull(&config)
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);

    // Assert
    let err = runtime
        .block_on(task)
        .expect_err("Expected runtime pull method to fail due to invalid image host.");

    match (err.kind(), err.cause().and_then(Fail::downcast_ref)) {
        (
            edgelet_docker::ErrorKind::RegistryOperation(
                edgelet_core::RegistryOperation::PullImage(name),
            ),
            Some(edgelet_docker::ErrorKind::FormattedDockerRuntime(message)),
        ) if name == INVALID_IMAGE_HOST => {
            assert_eq!(
                &format!(
                    "Get https://invalidhost.com: dial tcp: lookup {} on X.X.X.X: no such host",
                    INVALID_IMAGE_HOST
                ),
                message
            );
        }

        _ => panic!(
            "Specific docker runtime message is expected for invalid image host. Got {:?}",
            err.kind()
        ),
    }
}

#[allow(clippy::needless_pass_by_value)]
fn image_pull_with_invalid_creds_handler(req: Request<Body>) -> ResponseFuture {
    // verify that path is /images/create and that the "fromImage" query
    // parameter has the image name we expect
    assert_eq!(req.uri().path(), "/images/create");

    let query_map: HashMap<String, String> = parse_query(req.uri().query().unwrap().as_bytes())
        .into_owned()
        .collect();
    assert!(query_map.contains_key("fromImage"));
    assert_eq!(query_map.get("fromImage"), Some(&IMAGE_NAME.to_string()));

    // verify registry creds
    let auth_str = req
        .headers()
        .get_all("X-Registry-Auth")
        .into_iter()
        .map(|bytes| base64::decode(bytes).unwrap())
        .map(|raw| str::from_utf8(&raw).unwrap().to_owned())
        .collect::<String>();
    let auth_config: AuthConfig = serde_json::from_str(&auth_str).unwrap();
    assert_eq!(auth_config.username(), Some("u1"));
    assert_eq!(auth_config.password(), Some("wrong_password"));
    assert_eq!(auth_config.email(), Some("u1@bleh.com"));
    assert_eq!(auth_config.serveraddress(), Some("svr1"));

    let response = format!(
        r#"
    {{
        "message":"Get {}: unauthorized: authentication required"
    }}
    "#,
        IMAGE_NAME
    );
    let response_len = response.len();

    let mut response = Response::new(response.into());
    response
        .headers_mut()
        .typed_insert(&ContentLength(response_len as u64));
    response
        .headers_mut()
        .typed_insert(&ContentType(mime::APPLICATION_JSON));
    *response.status_mut() = hyper::StatusCode::INTERNAL_SERVER_ERROR;
    Box::new(future::ok(response))
}

#[test]
fn image_pull_with_invalid_creds_fails() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        POST "/images/create" => image_pull_with_invalid_creds_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| {
            let auth = AuthConfig::new()
                .with_username("u1".to_string())
                .with_password("wrong_password".to_string())
                .with_email("u1@bleh.com".to_string())
                .with_serveraddress("svr1".to_string());
            let config = DockerConfig::new(
                IMAGE_NAME.to_string(),
                ContainerCreateBody::new(),
                Some(auth),
            )
            .unwrap();

            runtime.pull(&config)
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);

    // Assert
    let err = runtime
        .block_on(task)
        .expect_err("Expected runtime pull method to fail due to unauthentication.");

    match (err.kind(), err.cause().and_then(Fail::downcast_ref)) {
        (
            edgelet_docker::ErrorKind::RegistryOperation(
                edgelet_core::RegistryOperation::PullImage(name),
            ),
            Some(edgelet_docker::ErrorKind::FormattedDockerRuntime(message)),
        ) if name == IMAGE_NAME => {
            assert_eq!(
                &format!(
                    "Get {}: unauthorized: authentication required",
                    &IMAGE_NAME.to_string()
                ),
                message
            );
        }

        _ => panic!(
            "Specific docker runtime message is expected for unauthentication. Got {:?}",
            err.kind()
        ),
    }
}

#[allow(clippy::needless_pass_by_value)]
fn image_pull_handler(req: Request<Body>) -> ResponseFuture {
    // verify that path is /images/create and that the "fromImage" query
    // parameter has the image name we expect
    assert_eq!(req.uri().path(), "/images/create");

    let query_map: HashMap<String, String> = parse_query(req.uri().query().unwrap().as_bytes())
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
    let response_len = response.len();

    let mut response = Response::new(response.into());
    response
        .headers_mut()
        .typed_insert(&ContentLength(response_len as u64));
    response
        .headers_mut()
        .typed_insert(&ContentType(mime::APPLICATION_JSON));
    Box::new(future::ok(response))
}

#[test]
fn image_pull_succeeds() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        POST "/images/create" => image_pull_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| {
            let auth = AuthConfig::new()
                .with_username("u1".to_string())
                .with_password("bleh".to_string())
                .with_email("u1@bleh.com".to_string())
                .with_serveraddress("svr1".to_string());
            let config = DockerConfig::new(
                IMAGE_NAME.to_string(),
                ContainerCreateBody::new(),
                Some(auth),
            )
            .unwrap();

            runtime.pull(&config)
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[allow(clippy::needless_pass_by_value)]
fn image_pull_with_creds_handler(req: Request<Body>) -> ResponseFuture {
    // verify that path is /images/create and that the "fromImage" query
    // parameter has the image name we expect
    assert_eq!(req.uri().path(), "/images/create");

    let query_map: HashMap<String, String> = parse_query(req.uri().query().unwrap().as_bytes())
        .into_owned()
        .collect();
    assert!(query_map.contains_key("fromImage"));
    assert_eq!(query_map.get("fromImage"), Some(&IMAGE_NAME.to_string()));

    // verify registry creds
    let auth_str = req
        .headers()
        .get_all("X-Registry-Auth")
        .into_iter()
        .map(|bytes| base64::decode(bytes).unwrap())
        .map(|raw| str::from_utf8(&raw).unwrap().to_owned())
        .collect::<String>();
    let auth_config: AuthConfig = serde_json::from_str(&auth_str).unwrap();
    assert_eq!(auth_config.username(), Some("u1"));
    assert_eq!(auth_config.password(), Some("bleh"));
    assert_eq!(auth_config.email(), Some("u1@bleh.com"));
    assert_eq!(auth_config.serveraddress(), Some("svr1"));

    let response = r#"
    {
        "Id": "img1",
        "Warnings": []
    }
    "#;
    let response_len = response.len();

    let mut response = Response::new(response.into());
    response
        .headers_mut()
        .typed_insert(&ContentLength(response_len as u64));
    response
        .headers_mut()
        .typed_insert(&ContentType(mime::APPLICATION_JSON));
    Box::new(future::ok(response))
}

#[test]
fn image_pull_with_creds_succeeds() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        POST "/images/create" => image_pull_with_creds_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| {
            let auth = AuthConfig::new()
                .with_username("u1".to_string())
                .with_password("bleh".to_string())
                .with_email("u1@bleh.com".to_string())
                .with_serveraddress("svr1".to_string());
            let config = DockerConfig::new(
                IMAGE_NAME.to_string(),
                ContainerCreateBody::new(),
                Some(auth),
            )
            .unwrap();

            runtime.pull(&config)
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[allow(clippy::needless_pass_by_value)]
fn image_remove_handler(req: Request<Body>) -> ResponseFuture {
    assert_eq!(req.method(), &Method::DELETE);
    assert_eq!(req.uri().path(), &format!("/images/{}", IMAGE_NAME));

    let response = serde_json::to_string(&vec![
        ImageDeleteResponseItem::new().with_deleted(IMAGE_NAME.to_string())
    ])
    .unwrap();
    let response_len = response.len();

    let mut response = Response::new(response.into());
    response
        .headers_mut()
        .typed_insert(&ContentLength(response_len as u64));
    response
        .headers_mut()
        .typed_insert(&ContentType(mime::APPLICATION_JSON));
    Box::new(future::ok(response))
}

#[test]
fn image_remove_succeeds() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        DELETE format!("/images/{}", IMAGE_NAME) => image_remove_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| ModuleRegistry::remove(&runtime, IMAGE_NAME));

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

fn container_create_handler(req: Request<Body>) -> ResponseFuture {
    assert_eq!(req.method(), &Method::POST);
    assert_eq!(req.uri().path(), "/containers/create");

    let response = json!({
        "Id": "12345",
        "Warnings": []
    })
    .to_string();
    let response_len = response.len();

    Box::new(
        req.into_body()
            .concat2()
            .and_then(|body| {
                let create_options: ContainerCreateBody =
                    serde_json::from_slice(body.as_ref()).unwrap();

                assert_eq!("nginx:latest", create_options.image().unwrap());

                for &v in &["/do/the/custom/command", "with these args"] {
                    assert!(create_options.cmd().unwrap().contains(&v.to_string()));
                }

                for &v in &["/also/do/the/entrypoint", "and this"] {
                    assert!(create_options
                        .entrypoint()
                        .unwrap()
                        .contains(&v.to_string()));
                }

                for &v in &["k1=v1", "k2=v2", "k3=v3", "k4=v4", "k5=v5"] {
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
                let mut expected = ::std::collections::HashMap::new();
                expected.insert("test1".to_string(), json!({}));
                assert_eq!(*volumes, expected);

                Ok(())
            })
            .map(move |_| {
                let mut response = Response::new(response.into());
                response
                    .headers_mut()
                    .typed_insert(&ContentLength(response_len as u64));
                response
                    .headers_mut()
                    .typed_insert(&ContentType(mime::APPLICATION_JSON));
                response
            }),
    )
}

#[test]
fn container_create_succeeds() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        POST "/containers/create" => container_create_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| {
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
            let memory: i64 = 3_221_225_472;
            let mut volumes = ::std::collections::HashMap::new();
            volumes.insert("test1".to_string(), json!({}));
            let create_options = ContainerCreateBody::new()
                .with_host_config(
                    HostConfig::new()
                        .with_port_bindings(port_bindings)
                        .with_memory(memory),
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
                "m1".to_string(),
                "docker".to_string(),
                DockerConfig::new("nginx:latest".to_string(), create_options, None).unwrap(),
                env,
                ImagePullPolicy::default(),
            )
            .unwrap();

            runtime.create(module_config)
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[allow(clippy::needless_pass_by_value)]
fn container_start_handler(req: Request<Body>) -> ResponseFuture {
    assert_eq!(req.method(), &Method::POST);
    assert_eq!(req.uri().path(), "/containers/m1/start");

    Box::new(future::ok(Response::new(Body::empty())))
}

#[test]
fn container_start_succeeds() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        POST "/containers/m1/start" => container_start_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.start("m1"));

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[allow(clippy::needless_pass_by_value)]
fn container_stop_handler(req: Request<Body>) -> ResponseFuture {
    assert_eq!(req.method(), &Method::POST);
    assert_eq!(req.uri().path(), "/containers/m1/stop");

    Box::new(future::ok(Response::new(Body::empty())))
}

#[test]
fn container_stop_succeeds() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        POST "/containers/m1/stop" => container_stop_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.stop("m1", None));

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[allow(clippy::needless_pass_by_value)]
fn container_stop_with_timeout_handler(req: Request<Body>) -> ResponseFuture {
    assert_eq!(req.method(), &Method::POST);
    assert_eq!(req.uri().path(), "/containers/m1/stop");
    assert_eq!(req.uri().query().unwrap(), "t=600");

    Box::new(future::ok(Response::new(Body::empty())))
}

#[test]
fn container_stop_with_timeout_succeeds() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        POST "/containers/m1/stop" => container_stop_with_timeout_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.stop("m1", Some(Duration::from_secs(600))));

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[allow(clippy::needless_pass_by_value)]
fn container_remove_handler(req: Request<Body>) -> ResponseFuture {
    assert_eq!(req.method(), &Method::DELETE);
    assert_eq!(req.uri().path(), "/containers/m1");

    Box::new(future::ok(Response::new(Body::empty())))
}

#[test]
fn container_remove_succeeds() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        DELETE "/containers/m1" => container_remove_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| ModuleRuntime::remove(&runtime, "m1"));

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[allow(clippy::needless_pass_by_value)]
fn container_list_handler(req: Request<Body>) -> ResponseFuture {
    assert_eq!(req.method(), &Method::GET);
    assert_eq!(req.uri().path(), "/containers/json");

    let query_map: HashMap<String, String> = parse_query(req.uri().query().unwrap().as_bytes())
        .into_owned()
        .collect();
    assert!(query_map.contains_key("filters"));
    assert_eq!(
        query_map.get("filters"),
        Some(
            &json!({
                "label": vec!["net.azure-devices.edge.owner=Microsoft.Azure.Devices.Edge.Agent"]
            })
            .to_string()
        )
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
            labels,
            "".to_string(),
            "".to_string(),
            ContainerHostConfig::new(""),
            ContainerNetworkSettings::new(HashMap::new()),
            vec![],
        ),
    ];

    let response = serde_json::to_string(&modules).unwrap();
    let response_len = response.len();

    let mut response = Response::new(response.into());
    response
        .headers_mut()
        .typed_insert(&ContentLength(response_len as u64));
    response
        .headers_mut()
        .typed_insert(&ContentType(mime::APPLICATION_JSON));
    Box::new(future::ok(response))
}

#[test]
fn container_list_succeeds() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        GET "/containers/json" => container_list_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.list());

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    let modules = runtime.block_on(task).unwrap();

    assert_eq!(3, modules.len());

    assert_eq!("m1", modules[0].name());
    assert_eq!("m2", modules[1].name());
    assert_eq!("m3", modules[2].name());

    assert_eq!("img1", modules[0].config().image_id().unwrap());
    assert_eq!("img2", modules[1].config().image_id().unwrap());
    assert_eq!("img3", modules[2].config().image_id().unwrap());

    assert_eq!("nginx:latest", modules[0].config().image());
    assert_eq!("ubuntu:latest", modules[1].config().image());
    assert_eq!("mongo:latest", modules[2].config().image());

    for module in modules {
        for i in 0..3 {
            assert_eq!(
                module
                    .config()
                    .create_options()
                    .labels()
                    .unwrap()
                    .get(&format!("l{}", i + 1)),
                Some(&format!("v{}", i + 1))
            );
        }
    }
}

#[allow(clippy::needless_pass_by_value)]
fn container_logs_handler(req: Request<Body>) -> ResponseFuture {
    assert_eq!(req.method(), &Method::GET);
    assert_eq!(req.uri().path(), "/containers/mod1/logs");

    let query_map: HashMap<String, String> = parse_query(req.uri().query().unwrap().as_bytes())
        .into_owned()
        .collect();
    assert!(query_map.contains_key("stdout"));
    assert!(query_map.contains_key("stderr"));
    assert!(query_map.contains_key("follow"));
    assert!(query_map.contains_key("tail"));
    assert_eq!("true", query_map["follow"]);
    assert_eq!("all", query_map["tail"]);
    assert_eq!("100000", query_map["since"]);

    let body = vec![
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0d, 0x52, 0x6f, 0x73, 0x65, 0x73, 0x20, 0x61,
        0x72, 0x65, 0x20, 0x72, 0x65, 0x64, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x76,
        0x69, 0x6f, 0x6c, 0x65, 0x74, 0x73, 0x20, 0x61, 0x72, 0x65, 0x20, 0x62, 0x6c, 0x75, 0x65,
    ];

    Box::new(future::ok(Response::new(body.into())))
}

#[test]
fn container_logs_succeeds() {
    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        GET "/containers/mod1/logs" => container_logs_handler,
    );

    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| {
            let options = LogOptions::new()
                .with_follow(true)
                .with_tail(LogTail::All)
                .with_since(100_000);

            runtime.logs("mod1", &options)
        });

    let expected_body = [
        0x01_u8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0d, 0x52, 0x6f, 0x73, 0x65, 0x73, 0x20,
        0x61, 0x72, 0x65, 0x20, 0x72, 0x65, 0x64, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10,
        0x76, 0x69, 0x6f, 0x6c, 0x65, 0x74, 0x73, 0x20, 0x61, 0x72, 0x65, 0x20, 0x62, 0x6c, 0x75,
        0x65,
    ];

    let assert = task.and_then(Stream::concat2).and_then(|b| {
        assert_eq!(&expected_body[..], b.as_ref());
        Ok(())
    });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(assert).unwrap();
}

#[test]
fn image_remove_with_white_space_name_fails() {
    let (server, port) = run_tcp_server("127.0.0.1", default_network_handler());
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));
    let image_name = "     ";

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| ModuleRegistry::remove(&runtime, image_name))
        .then(|res| match res {
            Ok(_) => Err("Expected error but got a result.".to_string()),
            Err(err) => match err.kind() {
                ErrorKind::RegistryOperation(RegistryOperation::RemoveImage(s))
                    if s == image_name =>
                {
                    Ok(())
                }
                kind => panic!(
                    "Expected `RegistryOperation(RemoveImage)` error but got {:?}.",
                    kind
                ),
            },
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn create_fails_for_non_docker_type() {
    let (server, port) = run_tcp_server("127.0.0.1", default_network_handler());
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));
    let name = "not_docker";

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| {
            let module_config = ModuleSpec::new(
                "m1".to_string(),
                name.to_string(),
                DockerConfig::new("nginx:latest".to_string(), ContainerCreateBody::new(), None)
                    .unwrap(),
                HashMap::new(),
                ImagePullPolicy::default(),
            )
            .unwrap();

            runtime.create(module_config)
        })
        .then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::InvalidModuleType(s) if s == name => Ok::<_, Error>(()),
                kind => panic!("Expected `InvalidModuleType` error but got {:?}.", kind),
            },
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn start_fails_for_empty_id() {
    let (server, port) = run_tcp_server("127.0.0.1", default_network_handler());
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));
    let name = "";

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.start(name))
        .then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(StartModule)` error but got {:?}.",
                    kind
                ),
            },
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn start_fails_for_white_space_id() {
    let (server, port) = run_tcp_server("127.0.0.1", default_network_handler());
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));
    let name = "      ";

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.start(name))
        .then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(StartModule)` error but got {:?}.",
                    kind
                ),
            },
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn stop_fails_for_empty_id() {
    let (server, port) = run_tcp_server("127.0.0.1", default_network_handler());
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));
    let name = "";

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.stop(name, None))
        .then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::StopModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(StopModule)` error but got {:?}.",
                    kind
                ),
            },
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn stop_fails_for_white_space_id() {
    let (server, port) = run_tcp_server("127.0.0.1", default_network_handler());
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));
    let name = "     ";

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.stop(name, None))
        .then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::StopModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(StopModule)` error but got {:?}.",
                    kind
                ),
            },
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn restart_fails_for_empty_id() {
    let (server, port) = run_tcp_server("127.0.0.1", default_network_handler());
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));
    let name = "";

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.restart(name))
        .then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::RestartModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(RestartModule)` error but got {:?}.",
                    kind
                ),
            },
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn restart_fails_for_white_space_id() {
    let (server, port) = run_tcp_server("127.0.0.1", default_network_handler());
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));
    let name = "      ";

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.restart(name))
        .then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::RestartModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(RestartModule)` error but got {:?}.",
                    kind
                ),
            },
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn remove_fails_for_empty_id() {
    let (server, port) = run_tcp_server("127.0.0.1", default_network_handler());
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));
    let name = "";

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| ModuleRuntime::remove(&runtime, name))
        .then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::RemoveModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(RemoveModule)` error but got {:?}.",
                    kind
                ),
            },
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn remove_fails_for_white_space_id() {
    let (server, port) = run_tcp_server("127.0.0.1", default_network_handler());
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));
    let name = "      ";

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| ModuleRuntime::remove(&runtime, name))
        .then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::RemoveModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(RemoveModule)` error but got {:?}.",
                    kind
                ),
            },
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn get_fails_for_empty_id() {
    let (server, port) = run_tcp_server("127.0.0.1", default_network_handler());
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));
    let name = "";

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.get(name))
        .then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(GetModule)` error but got {:?}.",
                    kind
                ),
            },
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn get_fails_for_white_space_id() {
    let (server, port) = run_tcp_server("127.0.0.1", default_network_handler());
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));
    let name = "    ";

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.get(name))
        .then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(GetModule)` error but got {:?}.",
                    kind
                ),
            },
        });

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();
}

#[test]
fn runtime_init_network_does_not_exist_create() {
    let list_got_called_lock = Arc::new(RwLock::new(false));
    let list_got_called_lock_cloned = list_got_called_lock.clone();

    let create_got_called_lock = Arc::new(RwLock::new(false));
    let create_got_called_lock_cloned = create_got_called_lock.clone();

    let network_handler = make_network_handler(
        move || {
            let mut list_got_called_w = list_got_called_lock.write().unwrap();
            *list_got_called_w = true;

            json!([]).to_string()
        },
        move |_| {
            let mut create_got_called_w = create_got_called_lock.write().unwrap();
            *create_got_called_w = true;
        },
    );

    let (server, port) = run_tcp_server("127.0.0.1", network_handler);
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    //act
    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto());

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();

    //assert
    assert_eq!(true, *list_got_called_lock_cloned.read().unwrap());
    assert_eq!(true, *create_got_called_lock_cloned.read().unwrap());
}

#[test]
fn network_ipv6_create() {
    let list_got_called_lock = Arc::new(RwLock::new(false));
    let list_got_called_lock_cloned = list_got_called_lock.clone();

    let create_got_called_lock = Arc::new(RwLock::new(false));
    let create_got_called_lock_cloned = create_got_called_lock.clone();

    let network_handler = make_network_handler(
        move || {
            let mut list_got_called_w = list_got_called_lock.write().unwrap();
            *list_got_called_w = true;

            json!([]).to_string()
        },
        move |req| {
            let mut create_got_called_w = create_got_called_lock.write().unwrap();
            *create_got_called_w = true;

            let task = req
                .into_body()
                .concat2()
                .map(|body| {
                    let network: NetworkConfig = serde_json::from_slice(&body).unwrap();
                    assert_eq!("my-network", network.name().as_str());
                    let ipam_config = network.IPAM().unwrap().config().unwrap();

                    let ipam_config_0 = ipam_config.get(0).unwrap();
                    assert_eq!(ipam_config_0["Gateway"], "172.18.0.1");
                    assert_eq!(ipam_config_0["Subnet"], "172.18.0.0/16");
                    assert_eq!(ipam_config_0["IPRange"], "172.18.0.0/16");

                    let ipam_config_1 = ipam_config.get(1).unwrap();
                    assert_eq!(ipam_config_1["Gateway"], "172.20.0.1");
                    assert_eq!(ipam_config_1["Subnet"], "172.20.0.0/16");
                    assert_eq!(ipam_config_1["IPRange"], "172.20.0.0/24");
                })
                .map_err(|err| panic!("{:?}", err));

            tokio::spawn(task).into_future().wait().unwrap();
        },
    );

    let (server, port) = run_tcp_server("127.0.0.1", network_handler);
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port),
            "network": {
                "name": "my-network",
                "ipv6": true,
                "ipam": {
                    "config": [
                        {
                            "gateway": "172.18.0.1",
                            "subnet": "172.18.0.0/16",
                            "ip_range": "172.18.0.0/16"
                        },
                        {
                            "gateway": "172.20.0.1",
                            "subnet": "172.20.0.0/16",
                            "ip_range": "172.20.0.0/24"
                        }
                    ]
                }
            }
        }
    })));

    //act
    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto());

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();

    //assert
    assert_eq!(true, *list_got_called_lock_cloned.read().unwrap());
    assert_eq!(true, *create_got_called_lock_cloned.read().unwrap());
}

#[test]
fn runtime_init_network_exist_do_not_create() {
    let list_got_called_lock = Arc::new(RwLock::new(false));
    let list_got_called_lock_cloned = list_got_called_lock.clone();

    let create_got_called_lock = Arc::new(RwLock::new(false));
    let create_got_called_lock_cloned = create_got_called_lock.clone();

    let network_handler = make_network_handler(
        move || {
            let mut list_got_called_w = list_got_called_lock.write().unwrap();
            *list_got_called_w = true;

            json!([
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
            ])
            .to_string()
        },
        move |_| {
            let mut create_got_called_w = create_got_called_lock.write().unwrap();
            *create_got_called_w = true;
        },
    );

    let (server, port) = run_tcp_server("127.0.0.1", network_handler);
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    //act
    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto());

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    runtime.block_on(task).unwrap();

    //assert
    assert_eq!(true, *list_got_called_lock_cloned.read().unwrap());
    assert_eq!(false, *create_got_called_lock_cloned.read().unwrap());
}

#[test]
fn runtime_system_info_succeeds() {
    let system_info_got_called_lock = Arc::new(RwLock::new(false));
    let system_info_got_called_lock_cloned = system_info_got_called_lock.clone();

    let on_system_info = move |req: Request<Body>| {
        let mut system_info_got_called_w = system_info_got_called_lock.write().unwrap();
        *system_info_got_called_w = true;

        assert_eq!(req.uri().path(), "/info");

        let response = json!(
                {
                    "OSType": "linux",
                    "Architecture": "x86_64",
                }
        )
        .to_string();
        let response_len = response.len();

        let mut response = Response::new(response.into());
        response
            .headers_mut()
            .typed_insert(&ContentLength(response_len as u64));
        response
            .headers_mut()
            .typed_insert(&ContentType(mime::APPLICATION_JSON));

        Box::new(future::ok(response)) as ResponseFuture
    };

    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        GET "/info" => on_system_info,
    );

    //act
    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.system_info());

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    let system_info = runtime.block_on(task).unwrap();

    //assert
    assert_eq!(true, *system_info_got_called_lock_cloned.read().unwrap());
    assert_eq!("linux", system_info.os_type());
    assert_eq!("x86_64", system_info.architecture());
}

#[test]
fn runtime_system_info_none_returns_unkown() {
    let system_info_got_called_lock = Arc::new(RwLock::new(false));
    let system_info_got_called_lock_cloned = system_info_got_called_lock.clone();

    let on_system_info = move |req: Request<Body>| {
        let mut system_info_got_called_w = system_info_got_called_lock.write().unwrap();
        *system_info_got_called_w = true;

        assert_eq!(req.uri().path(), "/info");

        let response = json!({}).to_string();
        let response_len = response.len();

        let mut response = Response::new(response.into());
        response
            .headers_mut()
            .typed_insert(&ContentLength(response_len as u64));
        response
            .headers_mut()
            .typed_insert(&ContentType(mime::APPLICATION_JSON));

        Box::new(future::ok(response)) as ResponseFuture
    };

    let dispatch_table = routes!(
        GET "/networks" => default_get_networks_handler(),
        POST "/networks/create" => default_create_network_handler(),
        GET "/info" => on_system_info,
    );

    //act
    let (server, port) = run_tcp_server(
        "127.0.0.1",
        make_req_dispatcher(dispatch_table, Box::new(not_found_handler)),
    );
    let server = server.map_err(|err| panic!(err));

    let settings = make_settings(Some(json!({
        "moby_runtime": {
            "uri": &format!("http://localhost:{}", port)
        }
    })));

    let task = DockerModuleRuntime::make_runtime(settings, provisioning_result(), crypto())
        .and_then(|runtime| runtime.system_info());

    let mut runtime = tokio::runtime::current_thread::Runtime::new().unwrap();
    runtime.spawn(server);
    let system_info = runtime.block_on(task).unwrap();

    //assert
    assert_eq!(true, *system_info_got_called_lock_cloned.read().unwrap());
    assert_eq!("Unknown", system_info.os_type());
    assert_eq!("Unknown", system_info.architecture());
}
