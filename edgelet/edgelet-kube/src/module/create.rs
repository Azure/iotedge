// Copyright (c) Microsoft. All rights reserved.

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
use crate::error::Error;
use crate::{ErrorKind, KubeModuleRuntime};

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
    S::Error: Fail,
    S::Future: Send,
{
    let runtime_for_sa = runtime.clone();
    let module_for_sa = module.clone();

    let runtime_for_deployment = runtime.clone();
    let module_for_deployment = module.clone();

    let module_name = module.name().to_string();

    create_or_update_service_account(&runtime, &module)
        .and_then(move |_| create_or_update_role_binding(&runtime_for_sa, &module_for_sa))
        .and_then(move |_| {
            create_or_update_deployment(&runtime_for_deployment, &module_for_deployment)
        })
        .map_err(|err| {
            Error::from(
                err.context(ErrorKind::RuntimeOperation(RuntimeOperation::CreateModule(
                    module_name,
                ))),
            )
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
    S::Error: Fail,
    S::Future: Send,
{
    spec_to_service_account(runtime.settings(), module)
        .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
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
    spec_to_role_binding(runtime.settings(), module)
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
    spec_to_deployment(runtime.settings(), module)
        .map_err(|err| Error::from(err.context(ErrorKind::KubeClient)))
        .map(|(name, new_deployment)| {
            let client_copy = runtime.client().clone();
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
    use hyper::Method;
    use maplit::btreemap;
    use tokio::runtime::Runtime;

    use docker::models::{AuthConfig, ContainerCreateBody, HostConfig, Mount};
    use edgelet_core::{ImagePullPolicy, ModuleSpec};
    use edgelet_docker::DockerConfig;
    use edgelet_test_utils::routes;
    use edgelet_test_utils::web::{make_req_dispatcher, HttpMethod, RequestHandler, RequestPath};

    use crate::module::create::{
        create_or_update_deployment, create_or_update_role_binding,
        create_or_update_service_account,
    };
    use crate::module::create_module;
    use crate::tests::{
        create_deployment_handler, create_runtime, create_service_account_handler,
        deployment_list_handler, empty_deployment_list_handler, empty_service_account_list_handler,
        make_settings, not_found_handler, replace_deployment_handler, replace_role_binding_handler,
        replace_service_account_handler, service_account_list_handler,
    };

    #[test]
    fn it_creates_new_deployment_if_does_not_exist() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET format!("/apis/apps/v1/namespaces/{}/deployments", settings.namespace()) => empty_deployment_list_handler(),
            POST format!("/apis/apps/v1/namespaces/{}/deployments", settings.namespace()) => create_deployment_handler(),
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

        let task = create_or_update_deployment(&runtime, &module);

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
            GET format!("/apis/apps/v1/namespaces/{}/deployments", settings.namespace()) => empty_deployment_list_handler(),
            POST format!("/apis/apps/v1/namespaces/{}/deployments", settings.namespace()) => create_deployment_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);
        let module = create_module_spec("edgeagent");

        let task = create_module(&runtime, &module);

        let mut runtime = Runtime::new().unwrap();
        runtime.block_on(task).unwrap();
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
