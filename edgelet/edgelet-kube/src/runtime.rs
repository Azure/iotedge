// Copyright (c) Microsoft. All rights reserved.

use std::cell::RefCell;
use std::sync::{Arc, Mutex};
use std::time::Duration;

use failure::Fail;
use futures::future::Either;
use futures::prelude::*;
use futures::{future, stream, Async, Future, Stream};
use hyper::client::HttpConnector;
use hyper::service::Service;
use hyper::{Body, Chunk as HyperChunk, Request};
use hyper_tls::HttpsConnector;

use edgelet_core::{
    AuthId, Authenticator, LogOptions, MakeModuleRuntime, ModuleRegistry, ModuleRuntime,
    ModuleRuntimeState, ModuleSpec, ProvisioningResult as CoreProvisioningResult, RuntimeOperation,
    SystemInfo,
};
use edgelet_docker::DockerConfig;
use kube_client::{
    get_config, Client as KubeClient, Error as KubeClientError, HttpClient, TokenSource, ValueToken,
};
use provisioning::ProvisioningResult;

use crate::convert::{auth_to_image_pull_secret, pod_to_module};
use crate::error::{Error, ErrorKind};
use crate::module::{authenticate, create_module, KubeModule};
use crate::settings::Settings;

pub struct KubeModuleRuntime<T, S> {
    client: Arc<Mutex<RefCell<KubeClient<T, S>>>>,
    settings: Settings,
}

impl<T, S> KubeModuleRuntime<T, S> {
    pub fn new(client: KubeClient<T, S>, settings: Settings) -> Self {
        KubeModuleRuntime {
            client: Arc::new(Mutex::new(RefCell::new(client))),
            settings,
        }
    }

    pub(crate) fn client(&self) -> Arc<Mutex<RefCell<KubeClient<T, S>>>> {
        self.client.clone()
    }

    pub(crate) fn settings(&self) -> &Settings {
        &self.settings
    }
}

// NOTE:
//  We are manually implementing Clone here for KubeModuleRuntime because
//  #[derive(Clone] will cause the compiler to implicitly require Clone on
//  T and S which don't really need to be Clone because we wrap it inside
//  an Arc (for the "client" field).
//
//  Requiring Clone on S in particular is problematic because we typically use
//  the kube_client::HttpClient struct for this type which does not (and cannot)
//  implement Clone.
impl<T, S> Clone for KubeModuleRuntime<T, S> {
    fn clone(&self) -> Self {
        KubeModuleRuntime {
            client: self.client().clone(),
            settings: self.settings().clone(),
        }
    }
}

impl<T, S> ModuleRegistry for KubeModuleRuntime<T, S>
where
    T: TokenSource + Send + 'static,
    S: Service + Send + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Into<KubeClientError>,
    S::Future: Send,
{
    type Error = Error;
    type PullFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type RemoveFuture = Box<dyn Future<Item = (), Error = Self::Error>>;
    type Config = DockerConfig;

    fn pull(&self, config: &Self::Config) -> Self::PullFuture {
        // Find and generate image pull secrets.
        if let Some(auth) = config.auth() {
            // Have authorization for this module spec, create this if it doesn't exist.
            let fut = auth_to_image_pull_secret(self.settings().namespace(), auth)
                .map_err(Error::from)
                .map(|(secret_name, pull_secret)| {
                    let client_copy = self.client.clone();
                    let namespace_copy = self.settings().namespace().to_owned();
                    self.client
                        .lock()
                        .expect("Unexpected lock error")
                        .borrow_mut()
                        .list_secrets(self.settings().namespace(), Some(secret_name.as_str()))
                        .map_err(Error::from)
                        .and_then(move |secrets| {
                            if let Some(current_secret) = secrets.items.into_iter().find(|secret| {
                                secret.metadata.as_ref().map_or(false, |meta| {
                                    meta.name.as_ref().map_or(false, |n| *n == secret_name)
                                })
                            }) {
                                if current_secret == pull_secret {
                                    Either::A(Either::A(future::ok(())))
                                } else {
                                    let f = client_copy
                                        .lock()
                                        .expect("Unexpected lock error")
                                        .borrow_mut()
                                        .replace_secret(
                                            namespace_copy.as_str(),
                                            secret_name.as_str(),
                                            &pull_secret,
                                        )
                                        .map_err(Error::from)
                                        .map(|_| ());

                                    Either::A(Either::B(f))
                                }
                            } else {
                                let f = client_copy
                                    .lock()
                                    .expect("Unexpected lock error")
                                    .borrow_mut()
                                    .create_secret(namespace_copy.as_str(), &pull_secret)
                                    .map_err(Error::from)
                                    .map(|_| ());

                                Either::B(f)
                            }
                        })
                })
                .into_future()
                .flatten();

            Box::new(fut)
        } else {
            Box::new(future::ok(()))
        }
    }

    fn remove(&self, _: &str) -> Self::RemoveFuture {
        Box::new(future::ok(()))
    }
}

impl MakeModuleRuntime
    for KubeModuleRuntime<ValueToken, HttpClient<HttpsConnector<HttpConnector>, Body>>
{
    type Config = DockerConfig;
    type Settings = Settings;
    type ProvisioningResult = ProvisioningResult;
    type ModuleRuntime = Self;
    type Error = Error;
    type Future = Box<dyn Future<Item = Self::ModuleRuntime, Error = Self::Error> + Send>;

    fn make_runtime(
        settings: Self::Settings,
        provisioning_result: Self::ProvisioningResult,
    ) -> Self::Future {
        let settings = settings
            .with_device_id(provisioning_result.device_id())
            .with_iot_hub_hostname(provisioning_result.hub_name());
        let fut = get_config()
            .map(|config| KubeModuleRuntime::new(KubeClient::new(config), settings))
            .map_err(Error::from)
            .into_future();

        Box::new(fut)
    }
}

impl<T, S> ModuleRuntime for KubeModuleRuntime<T, S>
where
    T: TokenSource + Send + 'static,
    S: Service + Send + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Into<KubeClientError>,
    S::Future: Send,
{
    type Error = Error;
    type Config = DockerConfig;
    type Module = KubeModule;
    type ModuleRegistry = Self;
    type Chunk = Chunk;
    type Logs = Logs;

    type CreateFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type GetFuture =
        Box<dyn Future<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
    type ListFuture = Box<dyn Future<Item = Vec<Self::Module>, Error = Self::Error> + Send>;
    type ListWithDetailsStream =
        Box<dyn Stream<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
    type LogsFuture = Box<dyn Future<Item = Self::Logs, Error = Self::Error> + Send>;
    type RemoveFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type RestartFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type StartFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type StopFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type SystemInfoFuture = Box<dyn Future<Item = SystemInfo, Error = Self::Error> + Send>;
    type RemoveAllFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;

    fn create(&self, module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
        Box::new(create_module(self, &module))
    }

    fn get(&self, _id: &str) -> Self::GetFuture {
        unimplemented!()
    }

    fn start(&self, _id: &str) -> Self::StartFuture {
        Box::new(future::ok(()))
    }

    fn stop(&self, _id: &str, _wait_before_kill: Option<Duration>) -> Self::StopFuture {
        Box::new(future::ok(()))
    }

    fn restart(&self, _id: &str) -> Self::RestartFuture {
        Box::new(future::ok(()))
    }

    fn remove(&self, _id: &str) -> Self::RemoveFuture {
        Box::new(future::ok(()))
    }

    fn system_info(&self) -> Self::SystemInfoFuture {
        // TODO: Implement this.
        Box::new(future::ok(SystemInfo::new(
            "linux".to_string(),
            "x86_64".to_string(),
        )))
    }

    fn list(&self) -> Self::ListFuture {
        let result = self
            .client
            .lock()
            .expect("Unexpected lock error")
            .borrow_mut()
            .list_pods(
                self.settings().namespace(),
                Some(&self.settings().device_hub_selector()),
            )
            .map_err(Error::from)
            .and_then(|pods| {
                pods.items
                    .into_iter()
                    .filter_map(|pod| pod_to_module(&pod))
                    .try_fold(vec![], |mut modules, module_result| {
                        module_result.map(|module| {
                            modules.push(module);
                            modules
                        })
                    })
                    .into_future()
            });

        Box::new(result)
    }

    fn list_with_details(&self) -> Self::ListWithDetailsStream {
        Box::new(stream::empty())
    }

    fn logs(&self, _id: &str, _options: &LogOptions) -> Self::LogsFuture {
        Box::new(future::ok(Logs("".to_string(), Body::empty())))
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        self
    }

    fn remove_all(&self) -> Self::RemoveAllFuture {
        Box::new(future::ok(()))
    }
}

impl<T, S> Authenticator for KubeModuleRuntime<T, S>
where
    T: TokenSource + Send + 'static,
    S: Service + Send + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<S::ResBody>,
    S::Error: Into<KubeClientError>,
    S::Future: Send,
{
    type Error = Error;
    type Request = Request<Body>;
    type AuthenticateFuture = Box<dyn Future<Item = AuthId, Error = Self::Error> + Send>;

    fn authenticate(&self, req: &Self::Request) -> Self::AuthenticateFuture {
        Box::new(authenticate(&self.clone(), req))
    }
}

#[derive(Debug)]
pub struct Logs(String, Body);

impl Stream for Logs {
    type Item = Chunk;
    type Error = Error;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        match self.1.poll() {
            Ok(Async::Ready(chunk)) => Ok(Async::Ready(chunk.map(Chunk))),
            Ok(Async::NotReady) => Ok(Async::NotReady),
            Err(err) => Err(Error::from(err.context(ErrorKind::RuntimeOperation(
                RuntimeOperation::GetModuleLogs(self.0.clone()),
            )))),
        }
    }
}

impl From<Logs> for Body {
    fn from(logs: Logs) -> Self {
        logs.1
    }
}

#[derive(Debug, Default)]
pub struct Chunk(HyperChunk);

impl IntoIterator for Chunk {
    type Item = u8;
    type IntoIter = <HyperChunk as IntoIterator>::IntoIter;

    fn into_iter(self) -> Self::IntoIter {
        self.0.into_iter()
    }
}

impl Extend<u8> for Chunk {
    fn extend<T>(&mut self, iter: T)
    where
        T: IntoIterator<Item = u8>,
    {
        self.0.extend(iter)
    }
}

impl AsRef<[u8]> for Chunk {
    fn as_ref(&self) -> &[u8] {
        self.0.as_ref()
    }
}
