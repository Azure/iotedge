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

use crate::convert::{
    auth_to_image_pull_secret, pod_to_module, spec_to_deployment, spec_to_service_account,
};
use crate::error::{Error, Result};
use crate::runtime::KubeRuntimeData;
use crate::{KubeModule, KubeModuleRuntime};
use edgelet_core::ModuleSpec;
use hyper::header::HeaderValue;

pub struct CreateModule {
    inner: Box<dyn Future<Item = (), Error = Error> + Send>,
}

impl Future for CreateModule {
    type Item = ();
    type Error = Error;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        self.inner.poll()
    }
}

impl CreateModule {
    pub fn new<T, S>(
        runtime: KubeModuleRuntime<T, S>,
        module: ModuleSpec<DockerConfig>,
    ) -> Box<CreateModule>
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

        let inner = CreateModule::create_or_update_role(runtime, module)
            .and_then(|_| {
                CreateModule::create_or_update_service_account(runtime_for_sa, module_for_sa)
            })
            .and_then(|_| {
                CreateModule::create_or_update_deployment(
                    runtime_for_deployment,
                    module_for_deployment,
                )
            });

        Box::new(CreateModule {
            inner: Box::new(inner),
        })
    }

    fn create_or_update_role<T, S>(
        runtime: KubeModuleRuntime<T, S>,
        module: ModuleSpec<DockerConfig>,
    ) -> Box<dyn Future<Item = (), Error = Error> + Send>
    where
        T: TokenSource + Send + 'static,
        S: Send + Service + 'static,
        S::ReqBody: From<Vec<u8>>,
        S::ResBody: Stream,
        Body: From<S::ResBody>,
        S::Error: Into<KubeClientError>,
        S::Future: Send,
    {
        let f = spec_to_service_account(&runtime, &module)
            .map_err(Error::from)
            .map(|new_service_account| {
                let client_copy = runtime.client().clone();
                let namespace_copy = runtime.namespace().to_owned();
                runtime
                    .client()
                    .lock()
                    .expect("Expected lock error")
                    .borrow_mut()
                    .list_service_accounts(
                        runtime.namespace(),
                        Some(module.name()),
                        Some(runtime.device_hub_selector()),
                    )
                    .map_err(Error::from)
                    .and_then(move |service_accounts| {
                        if let Some(current_service_account) =
                            service_accounts.items.into_iter().find(|service_account| {
                                service_account.metadata.as_ref().map_or(false, |meta| {
                                    meta.name.as_ref().map_or(false, |n| *n == module.name())
                                })
                            })
                        {
                            // found service account, if the service account doesn't match, replace it
                            if current_service_account == new_service_account {
                                Either::A(Either::A(future::ok(())))
                            } else {
                                let fut = client_copy
                                    .lock()
                                    .expect("Unexpected lock error")
                                    .borrow_mut()
                                    .replace_service_account(
                                        &namespace_copy,
                                        module.name(),
                                        &new_service_account,
                                    )
                                    .map_err(Error::from)
                                    .map(|_| ());
                                Either::A(Either::B(fut))
                            }
                        } else {
                            // not found - create it
                            let fut = client_copy
                                .lock()
                                .expect("Unexpected lock error")
                                .borrow_mut()
                                .create_service_account(
                                    namespace_copy.as_str(),
                                    &new_service_account,
                                )
                                .map_err(Error::from)
                                .map(|_| ());
                            Either::B(fut)
                        }
                    })
            })
            .into_future()
            .flatten();

        Box::new(f)
    }

    fn create_or_update_service_account<T, S>(
        runtime: KubeModuleRuntime<T, S>,
        module: ModuleSpec<DockerConfig>,
    ) -> Box<dyn Future<Item = (), Error = Error> + Send>
    where
        T: TokenSource + Send + 'static,
        S: Send + Service + 'static,
        S::ReqBody: From<Vec<u8>>,
        S::ResBody: Stream,
        Body: From<S::ResBody>,
        S::Error: Into<KubeClientError>,
        S::Future: Send,
    {
        let f = spec_to_service_account(&runtime, &module)
            .map_err(Error::from)
            .map(|new_service_account| {
                let client_copy = runtime.client().clone();
                let namespace_copy = runtime.namespace().to_owned();
                runtime
                    .client()
                    .lock()
                    .expect("Expected lock error")
                    .borrow_mut()
                    .list_service_accounts(
                        runtime.namespace(),
                        Some(module.name()),
                        Some(runtime.device_hub_selector()),
                    )
                    .map_err(Error::from)
                    .and_then(move |service_accounts| {
                        if let Some(current_service_account) =
                            service_accounts.items.into_iter().find(|service_account| {
                                service_account.metadata.as_ref().map_or(false, |meta| {
                                    meta.name.as_ref().map_or(false, |n| *n == module.name())
                                })
                            })
                        {
                            // found service account, if the service account doesn't match, replace it
                            if current_service_account == new_service_account {
                                Either::A(Either::A(future::ok(())))
                            } else {
                                let fut = client_copy
                                    .lock()
                                    .expect("Unexpected lock error")
                                    .borrow_mut()
                                    .replace_service_account(
                                        &namespace_copy,
                                        module.name(),
                                        &new_service_account,
                                    )
                                    .map_err(Error::from)
                                    .map(|_| ());
                                Either::A(Either::B(fut))
                            }
                        } else {
                            // not found - create it
                            let fut = client_copy
                                .lock()
                                .expect("Unexpected lock error")
                                .borrow_mut()
                                .create_service_account(
                                    namespace_copy.as_str(),
                                    &new_service_account,
                                )
                                .map_err(Error::from)
                                .map(|_| ());
                            Either::B(fut)
                        }
                    })
            })
            .into_future()
            .flatten();

        Box::new(f)
    }

    fn create_or_update_deployment<T, S>(
        runtime: KubeModuleRuntime<T, S>,
        module: ModuleSpec<DockerConfig>,
    ) -> Box<dyn Future<Item = (), Error = Error> + Send>
    where
        T: TokenSource + Send + 'static,
        S: Send + Service + 'static,
        S::ReqBody: From<Vec<u8>>,
        S::ResBody: Stream,
        Body: From<S::ResBody>,
        S::Error: Into<KubeClientError>,
        S::Future: Send,
    {
        let f = spec_to_deployment(&runtime, &module)
            .map_err(Error::from)
            .map(|(name, new_deployment)| {
                let client_copy = runtime.client().clone();
                let namespace_copy = runtime.namespace().to_owned();
                runtime
                    .client()
                    .lock()
                    .expect("Unexpected lock error")
                    .borrow_mut()
                    .list_deployments(
                        runtime.namespace(),
                        Some(&name),
                        Some(&runtime.device_hub_selector()),
                    )
                    .map_err(Error::from)
                    .and_then(move |deployments| {
                        if let Some(current_deployment) =
                            deployments.items.into_iter().find(|deployment| {
                                deployment.metadata.as_ref().map_or(false, |meta| {
                                    meta.name.as_ref().map_or(false, |n| *n == name)
                                })
                            })
                        {
                            // found deployment, if the deployment found doesn't match, replace it.
                            if current_deployment == new_deployment {
                                Either::A(Either::A(future::ok(())))
                            } else {
                                let fut = client_copy
                                    .lock()
                                    .expect("Unexpected lock error")
                                    .borrow_mut()
                                    .replace_deployment(
                                        namespace_copy.as_str(),
                                        name.as_str(),
                                        &new_deployment,
                                    )
                                    .map_err(Error::from)
                                    .map(|_| ());
                                Either::A(Either::B(fut))
                            }
                        } else {
                            // Not found - create it.
                            let fut = client_copy
                                .lock()
                                .expect("Unexpected lock error")
                                .borrow_mut()
                                .create_deployment(namespace_copy.as_str(), &new_deployment)
                                .map_err(Error::from)
                                .map(|_| ());
                            Either::B(fut)
                        }
                    })
            })
            .into_future()
            .flatten();
        Box::new(f)
    }
}
