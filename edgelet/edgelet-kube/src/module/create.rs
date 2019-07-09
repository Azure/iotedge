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
            runtime
                .client()
                .lock()
                .expect("Unexpected lock error")
                .borrow_mut()
                .replace_service_account(
                    runtime.settings().namespace(),
                    &name,
                    &new_service_account,
                )
                .map_err(Error::from)
                .map(|_| ())
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
