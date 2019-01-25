// Copyright (c) Microsoft. All rights reserved.

use std::cell::RefCell;
use std::sync::{Arc, Mutex};
use std::time::Duration;

use crate::constants::*;
use crate::convert::{auth_to_image_pull_secret, pod_to_module, spec_to_deployment};
use crate::error::{Error, ErrorKind, Result};
use crate::module::KubeModule;
use edgelet_core::{
    LogOptions, ModuleRegistry, ModuleRuntime, ModuleRuntimeState, ModuleSpec, RuntimeOperation,
    SystemInfo,
};
use edgelet_docker::DockerConfig;
use edgelet_utils::{ensure_not_empty_with_context, sanitize_dns_label};
use failure::Fail;
use futures::future::Either;
use futures::prelude::*;
use futures::{future, stream, Async, Future, Stream};
use hyper::client::HttpConnector;
use hyper::service::Service;
use hyper::{Body, Chunk as HyperChunk};
use hyper_tls::HttpsConnector;
use kube_client::{get_config, Client as KubeClient, HttpClient, ValueToken};

//use std::collections::BTreeMap;
use url::Url;

#[derive(Clone)]
pub struct DeviceIdentity {
    pub iot_hub_hostname: String,
    pub device_id: String,
    pub edge_hostname: String,
}

impl DeviceIdentity {
    pub fn new(
        iot_hub_hostname: String,
        device_id: String,
        edge_hostname: String,
    ) -> DeviceIdentity {
        DeviceIdentity {
            iot_hub_hostname,
            device_id,
            edge_hostname,
        }
    }
}

#[derive(Clone)]
pub struct ProxySettings {
    pub proxy_image: String,
    pub proxy_config_path: String,
    pub proxy_config_map_name: String,
}

impl ProxySettings {
    pub fn new(
        proxy_image: String,
        proxy_config_path: String,
        proxy_config_map_name: String,
    ) -> ProxySettings {
        ProxySettings {
            proxy_image,
            proxy_config_path,
            proxy_config_map_name,
        }
    }
}

#[derive(Clone)]
pub struct ServiceSettings {
    pub service_account_name: String,
    pub workload_uri: Url,
    pub management_uri: Url,
}

impl ServiceSettings {
    pub fn new(
        service_account_name: String,
        workload_uri: Url,
        management_uri: Url,
    ) -> ServiceSettings {
        ServiceSettings {
            service_account_name,
            workload_uri,
            management_uri,
        }
    }
}

#[derive(Clone)]
pub struct KubeModuleRuntime<S> {
    client: Arc<Mutex<RefCell<KubeClient<ValueToken, S>>>>,
    namespace: String,
    use_pvc: bool,
    iot_hub_hostname: String,
    device_id: String,
    edge_hostname: String,
    proxy_image: String,
    proxy_config_path: String,
    proxy_config_map_name: String,
    service_account_name: String,
    workload_uri: Url,
    management_uri: Url,
    device_hub_selector: String,
}

pub trait KubeRuntimeData {
    fn namespace(&self) -> &str;
    fn use_pvc(&self) -> bool;
    fn iot_hub_hostname(&self) -> &str;
    fn device_id(&self) -> &str;
    fn edge_hostname(&self) -> &str;
    fn proxy_image(&self) -> &str;
    fn proxy_config_path(&self) -> &str;
    fn proxy_config_map_name(&self) -> &str;
    fn service_account_name(&self) -> &str;
    fn workload_uri(&self) -> &Url;
    fn management_uri(&self) -> &Url;
}

impl<S> KubeRuntimeData for KubeModuleRuntime<S> {
    fn namespace(&self) -> &str {
        &self.namespace
    }
    fn use_pvc(&self) -> bool {
        self.use_pvc
    }
    fn iot_hub_hostname(&self) -> &str {
        &self.iot_hub_hostname
    }
    fn device_id(&self) -> &str {
        &self.device_id
    }
    fn edge_hostname(&self) -> &str {
        &self.edge_hostname
    }
    fn proxy_image(&self) -> &str {
        &self.proxy_image
    }
    fn proxy_config_path(&self) -> &str {
        &self.proxy_config_path
    }
    fn proxy_config_map_name(&self) -> &str {
        &self.proxy_config_map_name
    }
    fn service_account_name(&self) -> &str {
        &self.service_account_name
    }
    fn workload_uri(&self) -> &Url {
        &self.workload_uri
    }
    fn management_uri(&self) -> &Url {
        &self.management_uri
    }
}

impl KubeModuleRuntime<HttpClient<HttpsConnector<HttpConnector>, Body>> {
    pub fn new(
        namespace: String,
        use_pvc: bool,
        device_identity: DeviceIdentity,
        proxy_settings: ProxySettings,
        service_settings: ServiceSettings,
    ) -> Result<Self> {
        ensure_not_empty_with_context(&namespace, || {
            ErrorKind::InvalidRunTimeParameter(String::from("namespace"), namespace.clone())
        })?;
        ensure_not_empty_with_context(&device_identity.iot_hub_hostname, || {
            ErrorKind::InvalidRunTimeParameter(
                String::from("iot_hub_hostname"),
                device_identity.iot_hub_hostname.clone(),
            )
        })?;
        ensure_not_empty_with_context(&device_identity.device_id, || {
            ErrorKind::InvalidRunTimeParameter(
                String::from("device_id"),
                device_identity.device_id.clone(),
            )
        })?;
        ensure_not_empty_with_context(&device_identity.edge_hostname, || {
            ErrorKind::InvalidRunTimeParameter(
                String::from("edge_hostname"),
                device_identity.edge_hostname.clone(),
            )
        })?;
        ensure_not_empty_with_context(&proxy_settings.proxy_image, || {
            ErrorKind::InvalidRunTimeParameter(
                String::from("proxy_image"),
                proxy_settings.proxy_image.clone(),
            )
        })?;
        ensure_not_empty_with_context(&proxy_settings.proxy_config_path, || {
            ErrorKind::InvalidRunTimeParameter(
                String::from("proxy_config_path"),
                proxy_settings.proxy_config_path.clone(),
            )
        })?;
        ensure_not_empty_with_context(&proxy_settings.proxy_config_map_name, || {
            ErrorKind::InvalidRunTimeParameter(
                String::from("proxy_config_map_name"),
                proxy_settings.proxy_config_map_name.clone(),
            )
        })?;
        ensure_not_empty_with_context(&service_settings.service_account_name, || {
            ErrorKind::InvalidRunTimeParameter(
                String::from("service_account_name"),
                service_settings.service_account_name.clone(),
            )
        })?;
        let device_hub_selector = format!(
            "{}={},{}={}",
            EDGE_DEVICE_LABEL,
            sanitize_dns_label(&device_identity.device_id),
            EDGE_HUBNAME_LABEL,
            sanitize_dns_label(&device_identity.iot_hub_hostname)
        );

        Ok(KubeModuleRuntime {
            client: Arc::new(Mutex::new(RefCell::new(KubeClient::new(get_config()?)))),
            namespace,
            use_pvc,
            iot_hub_hostname: device_identity.iot_hub_hostname,
            device_id: device_identity.device_id,
            edge_hostname: device_identity.edge_hostname,
            proxy_image: proxy_settings.proxy_image,
            proxy_config_path: proxy_settings.proxy_config_path,
            proxy_config_map_name: proxy_settings.proxy_config_map_name,
            service_account_name: service_settings.service_account_name,
            workload_uri: service_settings.workload_uri,
            management_uri: service_settings.management_uri,
            device_hub_selector,
        })
    }
}

impl<S> ModuleRegistry for KubeModuleRuntime<S>
where
    S: Send + Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<<S as Service>::ResBody>,
    <S::ResBody as Stream>::Item: AsRef<[u8]>,
    <S::ResBody as Stream>::Error: Into<Error>,
    S::Error: Into<Error>,
    <<S as hyper::service::Service>::ResBody as futures::Stream>::Error:
        std::convert::Into<kube_client::Error>,
    <S as hyper::service::Service>::Error: std::convert::Into<kube_client::Error>,
    <S as hyper::service::Service>::Future: Send,
{
    type Error = Error;
    type PullFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type RemoveFuture = Box<Future<Item = (), Error = Self::Error>>;
    type Config = DockerConfig;

    fn pull(&self, config: &Self::Config) -> Self::PullFuture {
        // Find and generate image pull secrets.
        if let Some(auth) = config.auth() {
            // Have authorization for this module spec, create this if it doesn't exist.
            let fut = auth_to_image_pull_secret(self.namespace(), auth)
                .map_err(Error::from)
                .map(|(secret_name, pull_secret)| {
                    let client_copy = self.client.clone();
                    let namespace_copy = self.namespace().to_owned();
                    let fut = self
                        .client
                        .lock()
                        .expect("Unexpected lock error")
                        .borrow_mut()
                        .list_secrets(self.namespace(), Some(secret_name.as_str()))
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
                                            secret_name.as_str(),
                                            namespace_copy.as_str(),
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
                        });
                    Either::A(fut)
                })
                .unwrap_or_else(|err| Either::B(future::err(err)));

            Box::new(fut)
        } else {
            Box::new(future::ok(()))
        }
    }

    fn remove(&self, _: &str) -> Self::RemoveFuture {
        Box::new(future::ok(()))
    }
}

impl<S> ModuleRuntime for KubeModuleRuntime<S>
where
    S: Send + Service + 'static,
    S::ReqBody: From<Vec<u8>>,
    S::ResBody: Stream,
    Body: From<<S as Service>::ResBody>,
    <S::ResBody as Stream>::Item: AsRef<[u8]>,
    <S::ResBody as Stream>::Error: Into<Error>,
    S::Error: Into<Error>,
    <<S as hyper::service::Service>::ResBody as futures::Stream>::Error:
        std::convert::Into<kube_client::Error>,
    <S as hyper::service::Service>::Error: std::convert::Into<kube_client::Error>,
    <S as hyper::service::Service>::Future: Send,
{
    type Error = Error;
    type Config = DockerConfig;
    type Module = KubeModule;
    type ModuleRegistry = Self;
    type Chunk = Chunk;
    type Logs = Logs;

    type CreateFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type InitFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type ListFuture = Box<Future<Item = Vec<Self::Module>, Error = Self::Error> + Send>;
    type ListWithDetailsStream =
        Box<Stream<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
    type LogsFuture = Box<Future<Item = Self::Logs, Error = Self::Error> + Send>;
    type RemoveFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type RestartFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type StartFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type StopFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type SystemInfoFuture = Box<Future<Item = SystemInfo, Error = Self::Error> + Send>;
    type RemoveAllFuture = Box<Future<Item = (), Error = Self::Error> + Send>;

    fn init(&self) -> Self::InitFuture {
        Box::new(future::ok(()))
    }

    fn create(&self, module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
        let f = spec_to_deployment(self, &module)
            .map_err(Error::from)
            .map(|(name, new_deployment)| {
                let client_copy = self.client.clone();
                let namespace_copy = self.namespace().to_owned();
                let fut = self
                    .client
                    .lock()
                    .expect("Unexpected lock error")
                    .borrow_mut()
                    .list_deployments(
                        self.namespace(),
                        Some(&name),
                        Some(&self.device_hub_selector),
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
                                        name.as_str(),
                                        namespace_copy.as_str(),
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
                    });
                Either::A(fut)
            })
            .unwrap_or_else(|err| Either::B(future::err(err)));
        Box::new(f)
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
            .list_pods(&self.namespace, Some(&self.device_hub_selector))
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

#[cfg(test)]
mod tests {

    use super::*;
    use std::str::FromStr;
    use url::Url;

    #[test]
    fn runtime_new() {
        let namespace = String::from("my-namespace");
        let device_id = DeviceIdentity {
            iot_hub_hostname: String::from("iothostname"),
            device_id: String::from("my_device_id"),
            edge_hostname: String::from("edge-hostname"),
        };
        let proxy_info = ProxySettings {
            proxy_image: String::from("proxy-image"),
            proxy_config_path: String::from("proxy-confg-path"),
            proxy_config_map_name: String::from("config-volume"),
        };
        let service_info = ServiceSettings {
            service_account_name: String::from("iotedge"),
            workload_uri: Url::from_str("http://localhost:35000").unwrap(),
            management_uri: Url::from_str("http://localhost:35001").unwrap(),
        };

        let result = KubeModuleRuntime::new(
            String::default(),
            true,
            device_id.clone(),
            proxy_info.clone(),
            service_info.clone(),
        );

        assert!(result.is_err());

        let mut device_erred = device_id.clone();
        device_erred.iot_hub_hostname = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            true,
            device_erred,
            proxy_info.clone(),
            service_info.clone(),
        );
        assert!(result.is_err());

        let mut device_erred = device_id.clone();
        device_erred.device_id = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            true,
            device_erred,
            proxy_info.clone(),
            service_info.clone(),
        );
        assert!(result.is_err());

        let mut device_erred = device_id.clone();
        device_erred.edge_hostname = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            true,
            device_erred,
            proxy_info.clone(),
            service_info.clone(),
        );
        assert!(result.is_err());

        let mut proxy_erred = proxy_info.clone();
        proxy_erred.proxy_image = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            true,
            device_id.clone(),
            proxy_erred,
            service_info.clone(),
        );
        assert!(result.is_err());

        let mut proxy_erred = proxy_info.clone();
        proxy_erred.proxy_config_path = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            true,
            device_id.clone(),
            proxy_erred,
            service_info.clone(),
        );
        assert!(result.is_err());

        let mut proxy_erred = proxy_info.clone();
        proxy_erred.proxy_config_map_name = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            true,
            device_id.clone(),
            proxy_erred,
            service_info.clone(),
        );
        assert!(result.is_err());

        let mut service_erred = service_info.clone();
        service_erred.service_account_name = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            true,
            device_id.clone(),
            proxy_info.clone(),
            service_erred,
        );
        assert!(result.is_err());
    }
}
