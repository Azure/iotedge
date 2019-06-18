// Copyright (c) Microsoft. All rights reserved.

#![allow(unused_variables, unused_imports, dead_code)] // todo remove after

use futures::future::Either;
use futures::prelude::*;
use futures::{future, stream, Future, Stream};
use hyper::service::Service;
use hyper::{header, Body, Chunk as HyperChunk, Request};

use edgelet_docker::DockerConfig;
use edgelet_utils::{ensure_not_empty_with_context, log_failure, sanitize_dns_label};
use kube_client::{Client as KubeClient, Error as KubeClientError, TokenSource};

use crate::constants::EDGE_EDGE_AGENT_NAME;
use crate::convert::{
    auth_to_image_pull_secret, pod_to_module, spec_to_deployment, spec_to_role_binding,
    spec_to_service_account,
};
use crate::error::{Error, Result};
use crate::runtime::KubeRuntimeData;
use crate::{KubeModule, KubeModuleRuntime};
use edgelet_core::ModuleSpec;
use failure::Fail;
use hyper::header::HeaderValue;

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

    let inner = create_or_update_service_account(&runtime, &module)
        .and_then(move |_| create_or_update_role_binding(&runtime_for_sa, &module_for_sa))
        .and_then(move |_| {
            create_or_update_deployment(&runtime_for_deployment, &module_for_deployment)
        });

    Box::new(inner)
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
    let f = spec_to_service_account(runtime, module)
        .map_err(Error::from)
        .map(|(name, new_service_account)| {
            let namespace = runtime.namespace().to_owned();
            runtime
                .client()
                .lock()
                .expect("Unexpected lock error")
                .borrow_mut()
                .replace_service_account(&namespace, &name, &new_service_account)
                .map_err(Error::from)
                .map(|_| ())
        })
        .into_future()
        .flatten();

    Box::new(f)
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
    let f = spec_to_role_binding(runtime, module)
        .map_err(Error::from)
        .map(|(name, new_role_binding)| {
            // create new role only for edge agent
            if new_role_binding.metadata.as_ref().unwrap().name
                == Some(EDGE_EDGE_AGENT_NAME.to_owned())
            {
                let client_copy = runtime.client().clone();
                let namespace = runtime.namespace().to_owned();
                Either::A(
                    runtime
                        .client()
                        .lock()
                        .expect("Unexpected lock error")
                        .borrow_mut()
                        .replace_role_binding(&namespace, &name, &new_role_binding)
                        .map_err(Error::from)
                        .map(|_| ()),
                )
            } else {
                Either::B(future::ok(()))
            }
        })
        .into_future()
        .flatten();

    Box::new(f)
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
    let f = spec_to_deployment(runtime, module)
        .map_err(Error::from)
        .map(|(name, new_deployment)| {
            let namespace = runtime.namespace().to_owned();
            runtime
                .client()
                .lock()
                .expect("Unexpected lock error")
                .borrow_mut()
                .replace_deployment(&namespace, &name, &new_deployment)
                .map_err(Error::from)
                .map(|_| ())
        })
        .into_future()
        .flatten();

    Box::new(f)
}
