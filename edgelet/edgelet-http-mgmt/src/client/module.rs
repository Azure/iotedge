// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::str::FromStr;
use std::sync::Arc;
use std::time::Duration;

use failure::{Fail, ResultExt};
use futures::future::{self, FutureResult};
use futures::prelude::*;
use futures::stream;
use hyper::{Body, Chunk as HyperChunk, Client};
use management::apis::client::APIClient;
use management::apis::configuration::Configuration;
use management::models::{Config, ModuleDetails as HttpModuleDetails};
use serde_json;
use url::Url;

use edgelet_core::*;
use edgelet_core::{
    ModuleOperation, RuntimeOperation, SystemInfo as CoreSystemInfo, SystemResources, UrlExt,
};
use edgelet_docker::{self, DockerConfig};
use edgelet_http::{UrlConnector, API_VERSION};

use crate::error::{Error, ErrorKind};

pub struct ModuleClient {
    client: Arc<APIClient>,
}

impl ModuleClient {
    pub fn new(url: &Url) -> Result<Self, Error> {
        let client = Client::builder()
            .build(UrlConnector::new(url).context(ErrorKind::InitializeModuleClient)?);

        let base_path = url
            .to_base_path()
            .context(ErrorKind::InitializeModuleClient)?;
        let mut configuration = Configuration::new(client);
        configuration.base_path = base_path
            .to_str()
            .ok_or(ErrorKind::InitializeModuleClient)?
            .to_string();

        let scheme = url.scheme().to_string();
        configuration.uri_composer = Box::new(move |base_path, path| {
            Ok(UrlConnector::build_hyper_uri(&scheme, base_path, path)?)
        });

        let module_client = ModuleClient {
            client: Arc::new(APIClient::new(configuration)),
        };
        Ok(module_client)
    }
}

impl Clone for ModuleClient {
    fn clone(&self) -> Self {
        ModuleClient {
            client: self.client.clone(),
        }
    }
}

#[derive(Clone, Debug)]
pub struct ModuleDetails(HttpModuleDetails, ModuleConfig);

#[derive(Clone, Debug)]
pub struct ModuleConfig(String, Config);

impl fmt::Display for ModuleConfig {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        if let edgelet_docker::MODULE_TYPE = self.0.as_ref() {
            if let Ok(c) = serde_json::from_value::<DockerConfig>(self.1.settings().clone()) {
                write!(f, "{}", c.image())?;
            }
        }
        Ok(())
    }
}

impl Module for ModuleDetails {
    type Config = ModuleConfig;
    type Error = Error;
    type RuntimeStateFuture = FutureResult<ModuleRuntimeState, Self::Error>;

    fn name(&self) -> &str {
        self.0.name()
    }

    fn type_(&self) -> &str {
        self.0.type_()
    }

    fn config(&self) -> &Self::Config {
        &self.1
    }

    fn runtime_state(&self) -> Self::RuntimeStateFuture {
        future::result(runtime_status(&self.0))
    }
}

fn runtime_status(details: &HttpModuleDetails) -> Result<ModuleRuntimeState, Error> {
    let status = ModuleStatus::from_str(details.status().runtime_status().status())
        .context(ErrorKind::ModuleOperation(ModuleOperation::RuntimeState))?;
    let description = details
        .status()
        .runtime_status()
        .description()
        .map(ToOwned::to_owned);
    let exit_code = details
        .status()
        .exit_status()
        .and_then(|e| e.status_code().parse::<i64>().ok());
    let exit_time = details
        .status()
        .exit_status()
        .and_then(|e| e.exit_time().parse().ok());
    let start_time = details.status().start_time().and_then(|s| s.parse().ok());

    let state = ModuleRuntimeState::default()
        .with_status(status)
        .with_status_description(description)
        .with_exit_code(exit_code)
        .with_started_at(start_time)
        .with_finished_at(exit_time);
    Ok(state)
}

impl ModuleRegistry for ModuleClient {
    type Error = Error;
    type PullFuture = FutureResult<(), Self::Error>;
    type RemoveFuture = FutureResult<(), Self::Error>;
    type Config = ModuleConfig;

    fn pull(&self, _config: &Self::Config) -> Self::PullFuture {
        future::ok(())
    }

    fn remove(&self, _name: &str) -> Self::RemoveFuture {
        future::ok(())
    }
}

impl ModuleRuntime for ModuleClient {
    type Error = Error;
    type Config = ModuleConfig;
    type Module = ModuleDetails;
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
    type SystemInfoFuture = Box<dyn Future<Item = CoreSystemInfo, Error = Self::Error> + Send>;
    type SystemResourcesFuture =
        Box<dyn Future<Item = SystemResources, Error = Self::Error> + Send>;
    type RemoveAllFuture = Box<dyn Future<Item = (), Error = Self::Error> + Send>;

    fn create(&self, _module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
        unimplemented!()
    }

    fn get(&self, _id: &str) -> Self::GetFuture {
        unimplemented!()
    }

    fn start(&self, id: &str) -> Self::StartFuture {
        let id = id.to_string();

        let start = self
            .client
            .module_api()
            .start_module(&API_VERSION.to_string(), &id)
            .map_err(|err| {
                Error::from_mgmt_error(
                    err,
                    ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(id)),
                )
            })
            .then(|result| match result {
                Err(e) => match e.kind() {
                    ErrorKind::NotModified => Ok(()),
                    _ => Err(e),
                },
                other => other,
            });
        Box::new(start)
    }

    fn stop(&self, id: &str, _wait_before_kill: Option<Duration>) -> Self::StopFuture {
        let id = id.to_string();

        let stop = self
            .client
            .module_api()
            .stop_module(&API_VERSION.to_string(), &id)
            .map_err(|err| {
                Error::from_mgmt_error(
                    err,
                    ErrorKind::RuntimeOperation(RuntimeOperation::StopModule(id)),
                )
            })
            .then(|result| match result {
                Err(e) => match e.kind() {
                    ErrorKind::NotModified => Ok(()),
                    _ => Err(e),
                },
                other => other,
            });
        Box::new(stop)
    }

    fn restart(&self, id: &str) -> Self::RestartFuture {
        let id = id.to_string();

        let restart = self
            .client
            .module_api()
            .restart_module(&API_VERSION.to_string(), &id)
            .map_err(|err| {
                Error::from_mgmt_error(
                    err,
                    ErrorKind::RuntimeOperation(RuntimeOperation::RestartModule(id)),
                )
            })
            .then(|result| match result {
                Err(e) => match e.kind() {
                    ErrorKind::NotModified => Ok(()),
                    _ => Err(e),
                },
                other => other,
            });
        Box::new(restart)
    }

    fn remove(&self, _id: &str) -> Self::RemoveFuture {
        unimplemented!()
    }

    fn system_info(&self) -> Self::SystemInfoFuture {
        unimplemented!()
    }

    fn system_resources(&self) -> Self::SystemResourcesFuture {
        unimplemented!()
    }

    fn list(&self) -> Self::ListFuture {
        let modules = self
            .client
            .module_api()
            .list_modules(&API_VERSION.to_string())
            .map(|list| {
                list.modules()
                    .iter()
                    .cloned()
                    .map(|m| {
                        let type_ = m.type_().clone();
                        let config = m.config().clone();
                        ModuleDetails(m, ModuleConfig(type_, config))
                    })
                    .collect()
            })
            .map_err(|err| {
                Error::from_mgmt_error(
                    err,
                    ErrorKind::RuntimeOperation(RuntimeOperation::ListModules),
                )
            });
        Box::new(modules)
    }

    fn list_with_details(&self) -> Self::ListWithDetailsStream {
        let modules = self
            .client
            .module_api()
            .list_modules(&API_VERSION.to_string())
            .map_err(|err| {
                Error::from_mgmt_error(
                    err,
                    ErrorKind::RuntimeOperation(RuntimeOperation::ListModules),
                )
            })
            .map(|list| {
                let iter = list.modules().to_owned().into_iter().map(|m| {
                    let type_ = m.type_().clone();
                    let config = m.config().clone();
                    let runtime_state = runtime_status(&m)?;
                    let module = ModuleDetails(m, ModuleConfig(type_, config));
                    Ok((module, runtime_state))
                });
                stream::iter_result(iter)
            })
            .into_stream()
            .flatten();
        Box::new(modules)
    }

    fn logs(&self, id: &str, options: &LogOptions) -> Self::LogsFuture {
        let id = id.to_string();

        let tail = &options.tail().to_string();
        let result = self
            .client
            .module_api()
            .module_logs(
                &API_VERSION.to_string(),
                &id,
                options.follow(),
                tail,
                options.since(),
            )
            .then(|logs| match logs {
                Ok(logs) => Ok(Logs(id, logs)),
                Err(err) => Err(Error::from_mgmt_error(
                    err,
                    ErrorKind::RuntimeOperation(RuntimeOperation::GetModuleLogs(id)),
                )),
            });
        Box::new(result)
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        self
    }

    fn remove_all(&self) -> Self::RemoveAllFuture {
        let self_for_remove = self.clone();
        Box::new(self.list().and_then(move |list| {
            let n = list
                .into_iter()
                .map(move |c| <Self as ModuleRuntime>::remove(&self_for_remove, c.name()));
            future::join_all(n).map(|_| ())
        }))
    }
}

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

pub struct Chunk(HyperChunk);

impl AsRef<[u8]> for Chunk {
    fn as_ref(&self) -> &[u8] {
        self.0.as_ref()
    }
}
