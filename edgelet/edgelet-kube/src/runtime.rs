// Copyright (c) Microsoft. All rights reserved.

use std::time::Duration;

use crate::constants::*;
use crate::convert::pod_to_module;
use crate::convert::spec_to_deployment;
use crate::error::{Error, ErrorKind, Result};
use crate::module::KubeModule;
use edgelet_core::{
    LogOptions, ModuleRegistry, ModuleRuntime, ModuleRuntimeState, ModuleSpec, RuntimeOperation,
    SystemInfo,
};
use edgelet_docker::DockerConfig;
use edgelet_utils::{ensure_not_empty_with_context, sanitize_dns_label};
use failure::Fail;
use futures::prelude::*;
use futures::{future, stream, Async, Future, Stream};
use hyper::client::HttpConnector;
use hyper::service::Service;
use hyper::{Body, Chunk as HyperChunk};
use hyper_tls::HttpsConnector;
//use k8s_openapi::v1_10::apimachinery::pkg::apis::meta::v1 as api_meta;
use kube_client::{get_config, Client as KubeClient, HttpClient, ValueToken};
use std::cell::RefCell;
//use std::collections::BTreeMap;
use url::Url;

#[derive(Clone)]
pub struct DeviceIdentity {
    pub iot_hub_hostname: String,
    pub device_id: String,
    pub edge_hostname: String,
}

#[derive(Clone)]
pub struct ProxySettings {
    pub proxy_image: String,
    pub proxy_config_path: String,
    pub proxy_config_volume_name: String,
}

#[derive(Clone)]
pub struct ServiceSettings {
    pub service_account_name: String,
    pub workload_uri: Url,
    pub management_uri: Url,
}

#[derive(Clone)]
pub struct KubeModuleRuntime<S> {
    client: RefCell<KubeClient<ValueToken, S>>,
    namespace: String,
    iot_hub_hostname: String,
    device_id: String,
    edge_hostname: String,
    proxy_image: String,
    proxy_config_path: String,
    proxy_config_volume_name: String,
    service_account_name: String,
    workload_uri: Url,
    management_uri: Url,
    pod_selector: String,
}

impl KubeModuleRuntime<HttpClient<HttpsConnector<HttpConnector>, Body>> {
    pub fn new(
        namespace: String,
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
        ensure_not_empty_with_context(&proxy_settings.proxy_config_volume_name, || {
            ErrorKind::InvalidRunTimeParameter(
                String::from("proxy_config_volume_name"),
                proxy_settings.proxy_config_volume_name.clone(),
            )
        })?;
        ensure_not_empty_with_context(&service_settings.service_account_name, || {
            ErrorKind::InvalidRunTimeParameter(
                String::from("service_account_name"),
                service_settings.service_account_name.clone(),
            )
        })?;
        let pod_selector = format!(
            "{}={},{}={}",
            EDGE_DEVICE_LABEL,
            sanitize_dns_label(&device_identity.device_id),
            EDGE_HUBNAME_LABEL,
            sanitize_dns_label(&device_identity.iot_hub_hostname)
        );

        Ok(KubeModuleRuntime {
            client: RefCell::new(KubeClient::new(get_config()?)),
            namespace,
            iot_hub_hostname: device_identity.iot_hub_hostname,
            device_id: device_identity.device_id,
            edge_hostname: device_identity.edge_hostname,
            proxy_image: proxy_settings.proxy_image,
            proxy_config_path: proxy_settings.proxy_config_path,
            proxy_config_volume_name: proxy_settings.proxy_config_volume_name,
            service_account_name: service_settings.service_account_name,
            workload_uri: service_settings.workload_uri,
            management_uri: service_settings.management_uri,
            pod_selector,
        })
    }
}

impl<S> ModuleRegistry for KubeModuleRuntime<S> {
    type Error = Error;
    type PullFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type RemoveFuture = Box<Future<Item = (), Error = Self::Error>>;
    type Config = DockerConfig;

    fn pull(&self, _: &Self::Config) -> Self::PullFuture {
        Box::new(future::ok(()))
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
        Box::new(
            spec_to_deployment(&module)
                .map_err(Error::from)
                .map(|_| ())
                .into_future(),
        )
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
            .borrow_mut()
            .list_pods(&self.namespace, Some(&self.pod_selector))
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
            proxy_config_volume_name: String::from("config-volume"),
        };
        let service_info = ServiceSettings {
            service_account_name: String::from("iotedge"),
            workload_uri: Url::from_str("http://localhost:35000").unwrap(),
            management_uri: Url::from_str("http://localhost:35001").unwrap(),
        };

        let result = KubeModuleRuntime::new(
            String::default(),
            device_id.clone(),
            proxy_info.clone(),
            service_info.clone(),
        );

        assert!(result.is_err());

        let mut device_erred = device_id.clone();
        device_erred.iot_hub_hostname = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            device_erred,
            proxy_info.clone(),
            service_info.clone(),
        );
        assert!(result.is_err());

        let mut device_erred = device_id.clone();
        device_erred.device_id = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            device_erred,
            proxy_info.clone(),
            service_info.clone(),
        );
        assert!(result.is_err());

        let mut device_erred = device_id.clone();
        device_erred.edge_hostname = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            device_erred,
            proxy_info.clone(),
            service_info.clone(),
        );
        assert!(result.is_err());

        let mut proxy_erred = proxy_info.clone();
        proxy_erred.proxy_image = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            device_id.clone(),
            proxy_erred,
            service_info.clone(),
        );
        assert!(result.is_err());

        let mut proxy_erred = proxy_info.clone();
        proxy_erred.proxy_config_path = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            device_id.clone(),
            proxy_erred,
            service_info.clone(),
        );
        assert!(result.is_err());

        let mut proxy_erred = proxy_info.clone();
        proxy_erred.proxy_config_volume_name = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            device_id.clone(),
            proxy_erred,
            service_info.clone(),
        );
        assert!(result.is_err());

        let mut service_erred = service_info.clone();
        service_erred.service_account_name = String::default();
        let result = KubeModuleRuntime::new(
            namespace.clone(),
            device_id.clone(),
            proxy_info.clone(),
            service_erred,
        );
        assert!(result.is_err());
    }
}
