// Copyright (c) Microsoft. All rights reserved.

use std::cell::RefCell;
use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use std::time::Duration;

use failure::Fail;
use futures::prelude::*;
use futures::{future, stream, Async, Future, Stream};
use hyper::client::HttpConnector;
use hyper::service::Service;
use hyper::{Body, Chunk as HyperChunk, Request};
use hyper_tls::HttpsConnector;

use edgelet_core::{
    AuthId, Authenticator, GetTrustBundle, LogOptions, MakeModuleRuntime, ModuleRegistry,
    ModuleRuntime, ModuleRuntimeState, ModuleSpec, ProvisioningResult as CoreProvisioningResult,
    RuntimeOperation, SystemInfo, SystemResources,
};
use edgelet_docker::DockerConfig;
use kube_client::{get_config, Client as KubeClient, HttpClient, TokenSource, ValueToken};
use provisioning::ProvisioningResult;

use crate::convert::pod_to_module;
use crate::error::{Error, ErrorKind};
use crate::module::{authenticate, create_module, init_trust_bundle, KubeModule};
use crate::registry::create_image_pull_secrets;
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
            client: self.client(),
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
    S::Error: Fail,
    S::Future: Send,
{
    type Error = Error;
    type PullFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;
    type RemoveFuture = Box<dyn Future<Item = (), Error = Self::Error>>;
    type Config = DockerConfig;

    fn pull(&self, config: &Self::Config) -> Self::PullFuture {
        Box::new(create_image_pull_secrets(self, &config))
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
        crypto: impl GetTrustBundle + Send + 'static,
    ) -> Self::Future {
        let settings = settings
            .with_device_id(provisioning_result.device_id())
            .with_iot_hub_hostname(provisioning_result.hub_name());

        let fut = get_config()
            .map(|config| (config.clone(), KubeClient::new(config)))
            .map_err(|err| Error::from(err.context(ErrorKind::Initialization)))
            .map(|(config, mut client)| {
                client
                    .is_subject_allowed("nodes".to_string(), "list".to_string())
                    .map(|subject_review_status| {
                        settings.with_nodes_rbac(subject_review_status.allowed)
                    })
                    .map_err(|err| Error::from(err.context(ErrorKind::Initialization)))
                    .map(|settings| KubeModuleRuntime::new(KubeClient::new(config), settings))
                    .and_then(move |runtime| init_trust_bundle(&runtime, crypto).map(|_| runtime))
            })
            .into_future()
            .flatten();

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
    S::Error: Fail,
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
    type SystemResourcesFuture =
        Box<dyn Future<Item = SystemResources, Error = Self::Error> + Send>;
    type RemoveAllFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;

    fn create(&self, module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
        Box::new(create_module(self, module))
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
        #[derive(Debug, serde_derive::Serialize)]
        pub struct Architecture {
            name: String,
            nodes_count: u32,
        };
        let fut = if self.settings.has_nodes_rbac() {
            future::Either::A(
                self.client
                    .lock()
                    .expect("Unexpected lock error")
                    .borrow_mut()
                    .list_nodes()
                    .map_err(|err| {
                        Error::from(
                            err.context(ErrorKind::RuntimeOperation(RuntimeOperation::SystemInfo)),
                        )
                    })
                    .map(|nodes| {
                        // Accumulate the architectures and their node counts into a map
                        let architectures = nodes
                            .items
                            .into_iter()
                            .filter_map(|node| {
                                node.status.and_then(|status| {
                                    status.node_info.map(|info| info.architecture)
                                })
                            })
                            .fold(HashMap::new(), |mut architectures, current_arch| {
                                let count = architectures.entry(current_arch).or_insert(0);
                                *count += 1;
                                architectures
                            });

                        // Convert a map to a list of architectures
                        let architectures = architectures
                            .into_iter()
                            .map(|(name, count)| Architecture {
                                name,
                                nodes_count: count,
                            })
                            .collect::<Vec<Architecture>>();

                        SystemInfo::new(
                            "Kubernetes".to_string(),
                            serde_json::to_string(&architectures).unwrap(),
                        )
                    }),
            )
        } else {
            future::Either::B(future::ok(SystemInfo::new(
                "Kubernetes".to_string(),
                "Kubernetes".to_string(),
            )))
        };
        Box::new(fut)
    }

    fn system_resources(&self) -> Self::SystemResourcesFuture {
        // TODO: add support for system resources on k8s
        Box::new(future::ok(SystemResources::new(
            0,
            0,
            0.0,
            0,
            0,
            vec![],
            "".to_owned(),
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
            .map_err(|err| {
                Error::from(err.context(ErrorKind::RuntimeOperation(RuntimeOperation::ListModules)))
            })
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
    S::Error: Fail,
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

#[cfg(test)]
mod tests {
    use hyper::service::service_fn;
    use hyper::{Body, Method, Request, StatusCode};
    use maplit::btreemap;
    use serde_json::json;
    use tokio::runtime::Runtime;

    use edgelet_core::ModuleRuntime;
    use edgelet_test_utils::routes;
    use edgelet_test_utils::web::{
        make_req_dispatcher, HttpMethod, RequestHandler, RequestPath, ResponseFuture,
    };

    use crate::tests::{create_runtime, make_settings, not_found_handler, response};

    #[test]
    fn runtime_get_system_info() {
        let settings = make_settings(None);

        let dispatch_table = routes!(
            GET "/api/v1/nodes" => list_node_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);

        let task = runtime.system_info();

        let mut runtime = Runtime::new().unwrap();
        let info = runtime.block_on(task).unwrap();

        assert_eq!(
            info.architecture(),
            "[{\"name\":\"amd64\",\"nodes_count\":2}]"
        );
    }

    #[test]
    fn runtime_get_system_info_no_rbac() {
        let more_settings = json!({"has_nodes_rbac" : "false"});
        let settings = make_settings(Option::Some(more_settings));
        assert_eq!(settings.has_nodes_rbac(), false);
        let dispatch_table = routes!(
            GET "/api/v1/nodes" => list_node_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);

        let task = runtime.system_info();

        let mut runtime = Runtime::new().unwrap();
        let info = runtime.block_on(task).unwrap();

        assert_eq!(info.architecture(), "Kubernetes");
    }

    #[test]
    fn runtime_get_system_info_rbac_set() {
        let more_settings = json!({"has_nodes_rbac" : "true"});
        let settings = make_settings(Option::Some(more_settings));
        assert_eq!(settings.has_nodes_rbac(), true);
        let dispatch_table = routes!(
            GET "/api/v1/nodes" => list_node_handler(),
        );

        let handler = make_req_dispatcher(dispatch_table, Box::new(not_found_handler));
        let service = service_fn(handler);
        let runtime = create_runtime(settings, service);

        let task = runtime.system_info();

        let mut runtime = Runtime::new().unwrap();
        let info = runtime.block_on(task).unwrap();

        assert_eq!(
            info.architecture(),
            "[{\"name\":\"amd64\",\"nodes_count\":2}]"
        );
    }

    fn list_node_handler() -> impl Fn(Request<Body>) -> ResponseFuture + Clone {
        move |_| {
            response(StatusCode::OK, || {
                json!({
                    "kind" : "NodeList",
                    "items" : [
                        {
                            "kind" : "Node",
                            "status" :
                            {
                                "nodeInfo":
                                {
                                  "machineID": "5aedea612a1a481a9f967578995b2930",
                                  "systemUUID": "0331B348-6DBE-4344-BF93-6A3407C31879",
                                  "bootID": "e8c73b01-12e6-45d1-a008-aeb3b5ae4225",
                                  "kernelVersion": "4.15.0-1052-azure",
                                  "osImage": "Ubuntu 16.04.6 LTS",
                                  "containerRuntimeVersion": "docker://3.0.6",
                                  "kubeletVersion": "v1.13.10",
                                  "kubeProxyVersion": "v1.13.10",
                                  "operatingSystem": "linux",
                                  "architecture": "amd64"
                                },
                            }
                        },
                        {
                            "kind" : "Node",
                            "status" :
                            {
                                "nodeInfo":
                                {
                                  "machineID": "5aedea612a1a481a9f967578995b2930",
                                  "systemUUID": "0331B348-6DBE-4344-BF93-6A3407C31879",
                                  "bootID": "e8c73b01-12e6-45d1-a008-aeb3b5ae4225",
                                  "kernelVersion": "4.15.0-1052-azure",
                                  "osImage": "Ubuntu 16.04.6 LTS",
                                  "containerRuntimeVersion": "docker://3.0.6",
                                  "kubeletVersion": "v1.13.10",
                                  "kubeProxyVersion": "v1.13.10",
                                  "operatingSystem": "linux",
                                  "architecture": "amd64"
                                },
                            }
                        }
                    ]
                })
                .to_string()
            })
        }
    }
}
