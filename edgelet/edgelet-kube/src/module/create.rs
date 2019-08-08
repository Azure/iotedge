// Copyright (c) Microsoft. All rights reserved.

use futures::future::Either;
use futures::prelude::*;
use futures::{future, Future, Stream};
use hyper::service::Service;
use hyper::Body;

use edgelet_core::ModuleSpec;
use edgelet_docker::DockerConfig;
use kube_client::{Error as KubeClientError, TokenSource};

use crate::constants::EDGE_EDGE_AGENT_NAME;
use crate::convert::{spec_to_deployment, spec_to_role_binding, spec_to_service_account};
use crate::error::Error;
use crate::KubeModuleRuntime;

pub fn create_module<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    module: &ModuleSpec<DockerConfig>,
) -> impl Future<Item = (), Error = Error>
where
    T: TokenSource + Send + 'static,
    S: Service + Send + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Into<KubeClientError>,
    S::Future: Send,
{
    let runtime_for_sa = runtime.clone();
    let module_for_sa = module.clone();

    let runtime_for_deployment = runtime.clone();
    let module_for_deployment = module.clone();

    create_or_update_service_account(&runtime, &module)
        .and_then(move |_| create_or_update_role_binding(&runtime_for_sa, &module_for_sa))
        .and_then(move |_| {
            create_or_update_deployment(&runtime_for_deployment, &module_for_deployment)
        })
}

fn create_or_update_service_account<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    module: &ModuleSpec<DockerConfig>,
) -> impl Future<Item = (), Error = Error>
where
    T: TokenSource + Send + 'static,
    S: Send + Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Into<KubeClientError>,
    S::Future: Send,
{
    spec_to_service_account(runtime.settings(), module)
        .map_err(Error::from)
        .map(|(name, new_service_account)| {
            let client_copy = runtime.client().clone();
            let namespace_copy = runtime.settings().namespace().to_owned();

            runtime
                .client()
                .lock()
                .expect("Unexpected lock error")
                .borrow_mut()
                .list_service_accounts(
                    runtime.settings().namespace(),
                    Some(&name),
                    Some(&runtime.settings().device_hub_selector()),
                )
                .map_err(Error::from)
                .and_then(move |service_accounts| {
                    if let Some(current) =
                        service_accounts.items.into_iter().find(|service_account| {
                            service_account.metadata.as_ref().map_or(false, |meta| {
                                meta.name.as_ref().map_or(false, |n| *n == name)
                            })
                        })
                    {
                        if current == new_service_account {
                            Either::A(Either::A(future::ok(())))
                        } else {
                            let fut = client_copy
                                .lock()
                                .expect("Unexpected lock error")
                                .borrow_mut()
                                .replace_service_account(
                                    namespace_copy.as_str(),
                                    &name,
                                    &new_service_account,
                                )
                                .map_err(Error::from)
                                .map(|_| ());

                            Either::A(Either::B(fut))
                        }
                    } else {
                        let fut = client_copy
                            .lock()
                            .expect("Unexpected lock error")
                            .borrow_mut()
                            .create_service_account(namespace_copy.as_str(), &new_service_account)
                            .map_err(Error::from)
                            .map(|_| ());

                        Either::B(fut)
                    }
                })
        })
        .into_future()
        .flatten()
}

fn create_or_update_role_binding<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    module: &ModuleSpec<DockerConfig>,
) -> impl Future<Item = (), Error = Error>
where
    T: TokenSource + Send + 'static,
    S: Send + Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Into<KubeClientError>,
    S::Future: Send,
{
    spec_to_role_binding(runtime.settings(), module)
        .map_err(Error::from)
        .map(|(name, new_role_binding)| {
            // create new role only for edge agent
            if name == EDGE_EDGE_AGENT_NAME {
                Either::A(
                    runtime
                        .client()
                        .lock()
                        .expect("Unexpected lock error")
                        .borrow_mut()
                        .replace_role_binding(
                            runtime.settings().namespace(),
                            &name,
                            &new_role_binding,
                        )
                        .map_err(Error::from)
                        .map(|_| ()),
                )
            } else {
                Either::B(future::ok(()))
            }
        })
        .into_future()
        .flatten()
}

fn create_or_update_deployment<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    module: &ModuleSpec<DockerConfig>,
) -> impl Future<Item = (), Error = Error>
where
    T: TokenSource + Send + 'static,
    S: Send + Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Into<KubeClientError>,
    S::Future: Send,
{
    spec_to_deployment(runtime.settings(), module)
        .map_err(Error::from)
        .map(|(name, new_deployment)| {
            runtime
                .client()
                .lock()
                .expect("Unexpected lock error")
                .borrow_mut()
                .replace_deployment(runtime.settings().namespace(), &name, &new_deployment)
                .map_err(Error::from)
                .map(|_| ())
        })
        .into_future()
        .flatten()
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use futures::future;
    use hyper::service::{service_fn, Service};
    use hyper::{Body, Method, Request, Response, StatusCode};
    use maplit::btreemap;
    use native_tls::TlsConnector;
    use serde_json::json;
    use tokio::runtime::Runtime;
    use typed_headers::{mime, ContentLength, ContentType, HeaderMapExt};
    use url::Url;

    use docker::models::{AuthConfig, ContainerCreateBody, HostConfig, Mount};
    use edgelet_core::{ImagePullPolicy, ModuleSpec};
    use edgelet_docker::DockerConfig;
    use edgelet_test_utils::routes;
    use edgelet_test_utils::web::{
        make_req_dispatcher, HttpMethod, RequestHandler, RequestPath, ResponseFuture,
    };
    use kube_client::{Client as KubeClient, Config as KubeConfig, TokenSource};

    use crate::module::create::{
        create_or_update_deployment, create_or_update_role_binding,
        create_or_update_service_account,
    };
    use crate::module::create_module;
    use crate::tests::make_settings;
    use crate::{Error, KubeModuleRuntime, Settings};

    #[test]
    fn it_replaces_deployment() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            PUT format!("/apis/apps/v1/namespaces/{}/deployments/edgeagent", settings.namespace()) => replace_deployment_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let module = create_module_spec("edgeagent");

        let task = create_or_update_deployment(&runtime, &module);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    #[test]
    fn it_creates_or_updates_role_binding_for_edgeagent() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            PUT format!("/apis/rbac.authorization.k8s.io/v1/namespaces/{}/rolebindings/edgeagent", settings.namespace()) => replace_role_binding_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let module = create_module_spec("edgeagent");

        let task = create_or_update_role_binding(&runtime, &module);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    #[test]
    fn it_does_not_create_or_update_role_binding_for_another_module() {
        let settings = make_settings(None);

        let dispatch_table = btreemap!();

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let module = create_module_spec("temp-sensor");

        let task = create_or_update_role_binding(&runtime, &module);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    #[test]
    fn it_creates_new_service_account_if_does_not_exist() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/api/v1/namespaces/{}/serviceaccounts", settings.namespace()) => empty_service_account_list_handler(),
            POST format!("/api/v1/namespaces/{}/serviceaccounts", settings.namespace()) => create_service_account_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let module = create_module_spec("edgeagent");

        let task = create_or_update_service_account(&runtime, &module);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    #[test]
    fn it_updates_existing_service_account_if_exists() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/api/v1/namespaces/{}/serviceaccounts", settings.namespace()) => service_account_list_handler(),
            PUT format!("/api/v1/namespaces/{}/serviceaccounts/edgeagent", settings.namespace()) => replace_service_account_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let module = create_module_spec("edgeagent");

        let task = create_or_update_service_account(&runtime, &module);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    #[test]
    fn it_creates_all_required_resources() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/api/v1/namespaces/{}/serviceaccounts", settings.namespace()) => empty_service_account_list_handler(),
            POST format!("/api/v1/namespaces/{}/serviceaccounts", settings.namespace()) => create_service_account_handler(),
            PUT format!("/apis/rbac.authorization.k8s.io/v1/namespaces/{}/rolebindings/edgeagent", settings.namespace()) => replace_role_binding_handler(),
            PUT format!("/apis/apps/v1/namespaces/{}/deployments/edgeagent", settings.namespace()) => replace_deployment_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let module = create_module_spec("edgeagent");

        let task = create_module(&runtime, &module);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    fn create_service_account_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::CREATED, || {
                json!({
                    "kind": "ServiceAccount",
                    "apiVersion": "v1",
                    "metadata": {
                        "name": "edgeagent",
                        "namespace": "my-namespace",
                    }
                })
                .to_string()
            })
        }
    }

    fn empty_service_account_list_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "ServiceAccountList",
                    "apiVersion": "v1",
                    "items": []
                })
                .to_string()
            })
        }
    }

    fn service_account_list_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "ServiceAccountList",
                    "apiVersion": "v1",
                    "items": [
                        {
                            "metadata": {
                                "name": "edgeagent",
                                "namespace": "my-namespace",
                            }
                        }
                    ]
                })
                .to_string()
            })
        }
    }

    fn replace_service_account_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "ServiceAccount",
                    "apiVersion": "v1",
                    "metadata": {
                        "name": "edgeagent",
                        "namespace": "my-namespace",
                    }
                })
                .to_string()
            })
        }
    }

    fn replace_role_binding_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "RoleBinding",
                    "apiVersion": "rbac.authorization.k8s.io/v1",
                    "metadata": {
                        "name": "edgeagent",
                        "namespace": "my-namespace",
                    },
                    "subjects": [
                        {
                            "kind": "ServiceAccount",
                            "name": "edgeagent",
                            "namespace": "my-namespace"
                        }
                    ],
                    "roleRef": {
                        "apiGroup": "rbac.authorization.k8s.io",
                        "kind": "ClusterRole",
                        "name": "cluster-admin"
                    }
                })
                .to_string()
            })
        }
    }

    fn replace_deployment_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "Deployment",
                    "apiVersion": "apps/v1",
                    "metadata": {
                        "name": "edgeagent",
                        "namespace": "my-namespace",
                    },
                })
                .to_string()
            })
        }
    }

    fn response(
        status_code: StatusCode,
        response: impl Fn() -> String + Clone + Send + 'static,
    ) -> ResponseFuture {
        let response = response();
        let response_len = response.len();

        let mut response = Response::new(response.into());
        *response.status_mut() = status_code;
        response
            .headers_mut()
            .typed_insert(&ContentLength(response_len as u64));
        response
            .headers_mut()
            .typed_insert(&ContentType(mime::APPLICATION_JSON));

        Box::new(future::ok(response)) as ResponseFuture
    }

    fn not_found_handler(_: Request<Body>) -> ResponseFuture {
        let response = Response::builder()
            .status(StatusCode::NOT_FOUND)
            .body(Body::default())
            .unwrap();

        Box::new(future::ok(response))
    }

    fn create_module_spec(name: &str) -> ModuleSpec<DockerConfig> {
        let create_body = ContainerCreateBody::new()
            .with_host_config(
                HostConfig::new()
                    .with_binds(vec![String::from("/a:/b:ro"), String::from("/c:/d")])
                    .with_privileged(true)
                    .with_mounts(vec![
                        Mount::new()
                            .with__type(String::from("bind"))
                            .with_read_only(true)
                            .with_source(String::from("/e"))
                            .with_target(String::from("/f")),
                        Mount::new()
                            .with__type(String::from("bind"))
                            .with_source(String::from("/g"))
                            .with_target(String::from("/h")),
                        Mount::new()
                            .with__type(String::from("volume"))
                            .with_read_only(true)
                            .with_source(String::from("i"))
                            .with_target(String::from("/j")),
                        Mount::new()
                            .with__type(String::from("volume"))
                            .with_source(String::from("k"))
                            .with_target(String::from("/l")),
                    ]),
            )
            .with_labels({
                let mut labels = HashMap::<String, String>::new();
                labels.insert(String::from("label1"), String::from("value1"));
                labels.insert(String::from("label2"), String::from("value2"));
                labels
            });
        let auth_config = AuthConfig::new()
            .with_password(String::from("a password"))
            .with_username(String::from("USERNAME"))
            .with_serveraddress(String::from("REGISTRY"));
        ModuleSpec::new(
            name.to_string(),
            "docker".to_string(),
            DockerConfig::new("my-image:v1.0".to_string(), create_body, Some(auth_config)).unwrap(),
            {
                let mut env = HashMap::new();
                env.insert(String::from("a"), String::from("b"));
                env.insert(String::from("C"), String::from("D"));
                env
            },
            ImagePullPolicy::default(),
        )
        .unwrap()
    }

    fn create_runtime<S: Service>(
        settings: Settings,
        service: S,
    ) -> KubeModuleRuntime<TestTokenSource, S> {
        let client = KubeClient::with_client(get_config(), service);
        KubeModuleRuntime::new(client, settings)
    }

    fn get_config() -> KubeConfig<TestTokenSource> {
        KubeConfig::new(
            Url::parse("https://localhost:443").unwrap(),
            "/api".to_string(),
            TestTokenSource,
            TlsConnector::new().unwrap(),
        )
    }

    #[derive(Clone)]
    struct TestTokenSource;

    impl TokenSource for TestTokenSource {
        type Error = Error;

        fn get(&self) -> kube_client::error::Result<Option<String>> {
            Ok(None)
        }
    }
}
