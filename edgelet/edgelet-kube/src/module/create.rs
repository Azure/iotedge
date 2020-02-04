// Copyright (c) Microsoft. All rights reserved.

use std::convert::TryFrom;
use std::sync::Arc;

use failure::Fail;
use futures::future::Either;
use futures::prelude::*;
use futures::{future, Future, Stream};
use hyper::service::Service;
use hyper::Body;

use edgelet_core::{ModuleSpec, RuntimeOperation};
use edgelet_docker::DockerConfig;
use kube_client::TokenSource;

use crate::constants::EDGE_EDGE_AGENT_NAME;
use crate::convert::{spec_to_deployment, spec_to_role_binding, spec_to_service_account};
use crate::error::{Error, MissingMetadataReason};
use crate::{ErrorKind, KubeModuleOwner, KubeModuleRuntime};

pub fn create_module<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    module: ModuleSpec<DockerConfig>,
) -> impl Future<Item = (), Error = Error>
where
    T: TokenSource + Send + 'static,
    S: Service + Send + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Fail,
    S::Future: Send,
{
    let runtime = Arc::new(runtime.clone());
    let module = Arc::new(module);
    let module_name = module.name().to_string();

    get_module_owner(&runtime, "iotedged")
        .and_then({
            move |module_owner| {
                let owner = Arc::new(module_owner);

                create_or_update_service_account(&runtime, &module, &owner)
                    .and_then({
                        let runtime = runtime.clone();
                        let module = module.clone();
                        let owner = owner.clone();
                        move |_| create_or_update_role_binding(&runtime, &module, &owner)
                    })
                    .and_then({
                        let runtime = runtime.clone();
                        let module = module.clone();
                        let owner = owner.clone();
                        move |_| create_or_update_deployment(&runtime, &module, &owner)
                    })
            }
        })
        .map_err(|err| {
            Error::from(
                err.context(ErrorKind::RuntimeOperation(RuntimeOperation::CreateModule(
                    module_name,
                ))),
            )
        })
}

fn get_module_owner<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    module_name: &str,
) -> impl Future<Item = KubeModuleOwner, Error = Error>
where
    T: TokenSource + Send + 'static,
    S: Service + Send + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Fail,
    S::Future: Send,
{
    let module_name = module_name.to_string();
    runtime
        .client()
        .lock()
        .expect("Unexpected lock error")
        .borrow_mut()
        .list_deployments(
            runtime.settings().namespace(),
            Some(&module_name),
            Some(&runtime.settings().device_hub_selector()),
        )
        .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
        .map(move |deployments| {
            deployments
                .items
                .into_iter()
                .find(|deployment| {
                    deployment.metadata.as_ref().map_or(false, |meta| {
                        meta.name.as_ref().map_or(false, |n| *n == module_name)
                    })
                })
                .ok_or_else(|| {
                    Error::from(ErrorKind::MissingMetadata(
                        MissingMetadataReason::Deployment,
                    ))
                })
                .and_then(KubeModuleOwner::try_from)
        })
        .into_future()
        .flatten()
}

fn create_or_update_service_account<T, S>(
    runtime: &KubeModuleRuntime<T, S>,
    module: &ModuleSpec<DockerConfig>,
    module_owner: &KubeModuleOwner,
) -> impl Future<Item = (), Error = Error>
where
    T: TokenSource + Send + 'static,
    S: Send + Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Fail,
    S::Future: Send,
{
    spec_to_service_account(runtime.settings(), module, module_owner)
        .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
        .map(|(name, new_service_account)| {
            let client_copy = runtime.client();
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
                .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
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
                                    &namespace_copy,
                                    &name,
                                    &new_service_account,
                                )
                                .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
                                .map(|_| ());

                            Either::A(Either::B(fut))
                        }
                    } else {
                        let fut = client_copy
                            .lock()
                            .expect("Unexpected lock error")
                            .borrow_mut()
                            .create_service_account(&namespace_copy, &new_service_account)
                            .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
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
    module_owner: &KubeModuleOwner,
) -> impl Future<Item = (), Error = Error>
where
    T: TokenSource + Send + 'static,
    S: Send + Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Fail,
    S::Future: Send,
{
    spec_to_role_binding(runtime.settings(), module, module_owner)
        .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
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
                        .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
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
    module_owner: &KubeModuleOwner,
) -> impl Future<Item = (), Error = Error>
where
    T: TokenSource + Send + 'static,
    S: Send + Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Fail,
    S::Future: Send,
{
    spec_to_deployment(runtime.settings(), module, module_owner)
        .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
        .map(|(name, new_deployment)| {
            let client_copy = runtime.client();
            let namespace_copy = runtime.settings().namespace().to_owned();

            runtime
                .client()
                .lock()
                .expect("Unexpected lock error")
                .borrow_mut()
                .list_deployments(
                    runtime.settings().namespace(),
                    Some(&name),
                    Some(&runtime.settings().device_hub_selector()),
                )
                .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
                .and_then(move |deployments| {
                    if let Some(current) = deployments.items.into_iter().find(|deployment| {
                        deployment.metadata.as_ref().map_or(false, |meta| {
                            meta.name.as_ref().map_or(false, |n| *n == name)
                        })
                    }) {
                        if current == new_deployment {
                            Either::A(Either::A(future::ok(())))
                        } else {
                            let fut = client_copy
                                .lock()
                                .expect("Unexpected lock error")
                                .borrow_mut()
                                .replace_deployment(namespace_copy.as_str(), &name, &new_deployment)
                                .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
                                .map(|_| ());

                            Either::A(Either::B(fut))
                        }
                    } else {
                        let fut = client_copy
                            .lock()
                            .expect("Unexpected lock error")
                            .borrow_mut()
                            .create_deployment(namespace_copy.as_str(), &new_deployment)
                            .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
                            .map(|_| ());

                        Either::B(fut)
                    }
                })
        })
        .into_future()
        .flatten()
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use hyper::service::service_fn;
    use hyper::{Body, Method, Request, StatusCode};
    use maplit::btreemap;
    use serde_json::json;
    use tokio::runtime::Runtime;

    use docker::models::{AuthConfig, ContainerCreateBody, HostConfig, Mount};
    use edgelet_core::{ImagePullPolicy, ModuleSpec, RuntimeOperation};
    use edgelet_docker::DockerConfig;
    use edgelet_test_utils::routes;
    use edgelet_test_utils::web::{
        make_req_dispatcher, HttpMethod, RequestHandler, RequestPath, ResponseFuture,
    };

    use crate::error::ErrorKind::RuntimeOperation as RuntimeOperationErrorKind;
    use crate::module::create::{
        create_or_update_deployment, create_or_update_role_binding,
        create_or_update_service_account,
    };
    use crate::module::create_module;
    use crate::tests::{
        create_module_owner, create_runtime, make_settings, not_found_handler, response,
    };

    #[test]
    fn it_creates_new_deployment_if_does_not_exist() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/apis/apps/v1/namespaces/{}/deployments", settings.namespace()) => noagent_deployment_list_handler(),
            POST format!("/apis/apps/v1/namespaces/{}/deployments", settings.namespace()) => create_deployment_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let module = create_module_spec("edgeagent");
        let module_owner = create_module_owner();

        let task = create_or_update_deployment(&runtime, &module, &module_owner);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    #[test]
    fn it_updates_existing_deployment_if_exists() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/apis/apps/v1/namespaces/{}/deployments", settings.namespace()) => deployment_list_handler(),
            PUT format!("/apis/apps/v1/namespaces/{}/deployments/edgeagent", settings.namespace()) => replace_deployment_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let module = create_module_spec("edgeagent");
        let module_owner = create_module_owner();

        let task = create_or_update_deployment(&runtime, &module, &module_owner);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    #[test]
    fn it_replaces_role_binding_for_edgeagent() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            PUT format!("/apis/rbac.authorization.k8s.io/v1/namespaces/{}/rolebindings/edgeagent", settings.namespace()) => replace_role_binding_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let module = create_module_spec("edgeagent");
        let module_owner = create_module_owner();

        let task = create_or_update_role_binding(&runtime, &module, &module_owner);

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
        let module_owner = create_module_owner();

        let task = create_or_update_role_binding(&runtime, &module, &module_owner);

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
        let module_owner = create_module_owner();

        let task = create_or_update_service_account(&runtime, &module, &module_owner);

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
        let module_owner = create_module_owner();

        let task = create_or_update_service_account(&runtime, &module, &module_owner);

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
            GET format!("/apis/apps/v1/namespaces/{}/deployments", settings.namespace()) => noagent_deployment_list_handler(),
            POST format!("/apis/apps/v1/namespaces/{}/deployments", settings.namespace()) => create_deployment_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let module = create_module_spec("edgeagent");

        let task = create_module(&runtime, module);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
    }

    #[test]
    fn it_fails_if_iotedged_deployment_is_missing() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/apis/apps/v1/namespaces/{}/deployments", settings.namespace()) => empty_deployment_list_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let module = create_module_spec("edgeagent");

        let task = create_module(&runtime, module);
        let mut runtime = Runtime::new().unwrap();
        let err = runtime.block_on(task).unwrap_err();
        assert_eq!(
            err.kind(),
            &RuntimeOperationErrorKind(RuntimeOperation::CreateModule("edgeagent".to_string()))
        );
    }

    fn empty_deployment_list_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "DeploymentList",
                    "apiVersion": "apps/v1"
                })
                .to_string()
            })
        }
    }

    fn noagent_deployment_list_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "DeploymentList",
                    "apiVersion": "apps/v1",
                    "items": [
                        {
                            "metadata": {
                                "name": "iotedged",
                                "namespace": "my-namespace",
                                "uid":"75d1a6a6-6bc9-4e80-906b-73fec80020ec",
                            }
                        }
                    ]
                })
                .to_string()
            })
        }
    }

    fn deployment_list_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind": "DeploymentList",
                    "apiVersion": "apps/v1",
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

    fn create_deployment_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::CREATED, || {
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
}
