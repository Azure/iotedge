// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::convert::From;
use std::ops::Deref;
use std::time::Duration;

use base64;
use failure::{Fail, ResultExt};
use futures::prelude::*;
use futures::{future, stream, Async, Stream};
use hyper::{Body, Chunk as HyperChunk, Client};
use log::Level;
use serde_json;
use url::Url;

use client::DockerClient;
use config::DockerConfig;
use docker::apis::client::APIClient;
use docker::apis::configuration::Configuration;
use docker::models::{ContainerCreateBody, InlineResponse200, InlineResponse2001, NetworkConfig};
use edgelet_core::{
    pid::Pid, LogOptions, Module, ModuleRegistry, ModuleRuntime, ModuleRuntimeState, ModuleSpec,
    ModuleTop, RegistryOperation, RuntimeOperation, SystemInfo as CoreSystemInfo, UrlExt,
};
use edgelet_http::UrlConnector;
use edgelet_utils::{ensure_not_empty_with_context, log_failure};

use error::{Error, ErrorKind, Result};
use module::{runtime_state, DockerModule, MODULE_TYPE as DOCKER_MODULE_TYPE};

type Deserializer = &'static mut serde_json::Deserializer<serde_json::de::IoRead<std::io::Empty>>;

const WAIT_BEFORE_KILL_SECONDS: i32 = 10;

static LABEL_KEY: &str = "net.azure-devices.edge.owner";
static LABEL_VALUE: &str = "Microsoft.Azure.Devices.Edge.Agent";

lazy_static! {
    static ref LABELS: Vec<&'static str> = {
        let mut labels = vec![];
        labels.push("net.azure-devices.edge.owner=Microsoft.Azure.Devices.Edge.Agent");
        labels
    };
}

#[derive(Clone)]
pub struct DockerModuleRuntime {
    client: DockerClient<UrlConnector>,
    network_id: Option<String>,
}

impl DockerModuleRuntime {
    pub fn new(docker_url: &Url) -> Result<Self> {
        // build the hyper client
        let client = Client::builder()
            .build(UrlConnector::new(docker_url).context(ErrorKind::Initialization)?);

        // extract base path - the bit that comes after the scheme
        let base_path = docker_url
            .to_base_path()
            .context(ErrorKind::Initialization)?;
        let mut configuration = Configuration::new(client);
        configuration.base_path = base_path
            .to_str()
            .ok_or(ErrorKind::Initialization)?
            .to_string();

        let scheme = docker_url.scheme().to_string();
        configuration.uri_composer = Box::new(move |base_path, path| {
            Ok(UrlConnector::build_hyper_uri(&scheme, base_path, path)
                .context(ErrorKind::Initialization)?)
        });

        Ok(DockerModuleRuntime {
            client: DockerClient::new(APIClient::new(configuration)),
            network_id: None,
        })
    }

    pub fn with_network_id(mut self, network_id: String) -> Self {
        self.network_id = Some(network_id);
        self
    }

    fn merge_env(cur_env: Option<&[String]>, new_env: &HashMap<String, String>) -> Vec<String> {
        // build a new merged hashmap containing string slices for keys and values
        // pointing into String instances in new_env
        let mut merged_env = HashMap::new();
        merged_env.extend(new_env.iter().map(|(k, v)| (k.as_str(), v.as_str())));

        if let Some(env) = cur_env {
            // extend merged_env with variables in cur_env (again, these are
            // only string slices pointing into strings inside cur_env)
            merged_env.extend(env.iter().filter_map(|s| {
                let mut tokens = s.splitn(2, '=');
                tokens.next().map(|key| (key, tokens.next().unwrap_or("")))
            }));
        }

        // finally build a new Vec<String>; we alloc new strings here
        merged_env
            .iter()
            .map(|(key, value)| format!("{}={}", key, value))
            .collect()
    }
}

impl ModuleRegistry for DockerModuleRuntime {
    type Error = Error;
    type PullFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type RemoveFuture = Box<Future<Item = (), Error = Self::Error>>;
    type Config = DockerConfig;

    fn pull(&self, config: &Self::Config) -> Self::PullFuture {
        let image = config.image().to_string();

        info!("Pulling image {}...", image);

        let creds: Result<String> = config.auth().map_or_else(
            || Ok("".to_string()),
            |a| {
                let json = serde_json::to_string(a).with_context(|_| {
                    ErrorKind::RegistryOperation(RegistryOperation::PullImage(image.clone()))
                })?;
                Ok(base64::encode(&json))
            },
        );

        let response = creds
            .map(|creds| {
                self.client
                    .image_api()
                    .image_create(&image, "", "", "", "", &creds, "")
                    .then(|result| match result {
                        Ok(()) => Ok(image),
                        Err(err) => Err(Error::from_docker_error(
                            err,
                            ErrorKind::RegistryOperation(RegistryOperation::PullImage(image)),
                        )),
                    })
            })
            .into_future()
            .flatten()
            .then(move |result| match result {
                Ok(image) => {
                    info!("Successfully pulled image {}", image);
                    Ok(())
                }
                Err(err) => {
                    log_failure(Level::Warn, &err);
                    Err(err)
                }
            });

        Box::new(response)
    }

    fn remove(&self, name: &str) -> Self::RemoveFuture {
        info!("Removing image {}...", name);

        if let Err(err) = ensure_not_empty_with_context(name, || {
            ErrorKind::RegistryOperation(RegistryOperation::RemoveImage(name.to_string()))
        }) {
            return Box::new(future::err(Error::from(err)));
        }

        let name = name.to_string();

        Box::new(
            self.client
                .image_api()
                .image_delete(&name, false, false)
                .then(|result| match result {
                    Ok(_) => {
                        info!("Successfully removed image {}", name);
                        Ok(())
                    }
                    Err(err) => {
                        let err = Error::from_docker_error(
                            err,
                            ErrorKind::RegistryOperation(RegistryOperation::RemoveImage(name)),
                        );
                        log_failure(Level::Warn, &err);
                        Err(err)
                    }
                }),
        )
    }
}

fn parse_get_response<'de, D>(resp: &InlineResponse200) -> std::result::Result<String, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let name = resp
        .name()
        .map(ToOwned::to_owned)
        .ok_or_else(|| serde::de::Error::missing_field("Name"))?;
    Ok(name)
}

fn parse_top_response<'de, D>(resp: &InlineResponse2001) -> std::result::Result<Vec<Pid>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let titles = resp
        .titles()
        .ok_or_else(|| serde::de::Error::missing_field("Titles"))?;
    let pid_index = titles
        .iter()
        .position(|ref s| s.as_str() == "PID")
        .ok_or_else(|| {
            serde::de::Error::invalid_value(
                serde::de::Unexpected::Seq,
                &"array including the column title 'PID'",
            )
        })?;
    let processes = resp
        .processes()
        .ok_or_else(|| serde::de::Error::missing_field("Processes"))?;
    let pids: std::result::Result<_, _> = processes
        .iter()
        .map(|ref p| {
            let val = p.get(pid_index).ok_or_else(|| {
                serde::de::Error::invalid_length(
                    p.len(),
                    &&*format!("at least {} columns", pid_index + 1),
                )
            })?;
            let pid = val.parse::<i32>().map_err(|_| {
                serde::de::Error::invalid_value(
                    serde::de::Unexpected::Str(val),
                    &"a process ID number",
                )
            })?;
            Ok(Pid::Value(pid))
        })
        .collect();
    Ok(pids?)
}

impl ModuleRuntime for DockerModuleRuntime {
    type Error = Error;
    type Config = DockerConfig;
    type Module = DockerModule<UrlConnector>;
    type ModuleRegistry = Self;
    type Chunk = Chunk;
    type Logs = Logs;

    type CreateFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type GetFuture =
        Box<Future<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
    type InitFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type ListFuture = Box<Future<Item = Vec<Self::Module>, Error = Self::Error> + Send>;
    type ListWithDetailsStream =
        Box<Stream<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
    type LogsFuture = Box<Future<Item = Self::Logs, Error = Self::Error> + Send>;
    type RemoveFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type RestartFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type StartFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type StopFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type SystemInfoFuture = Box<Future<Item = CoreSystemInfo, Error = Self::Error> + Send>;
    type RemoveAllFuture = Box<Future<Item = (), Error = Self::Error> + Send>;
    type TopFuture = Box<Future<Item = ModuleTop, Error = Self::Error> + Send>;

    fn init(&self) -> Self::InitFuture {
        info!("Initializing module runtime...");

        let created = self.network_id.clone().map_or_else(
            || future::Either::B(future::ok(())),
            |id| {
                let filter = format!(r#"{{"name":{{"{}":true}}}}"#, id);
                let client_copy = self.client.clone();
                let fut = self
                    .client
                    .network_api()
                    .network_list(&filter)
                    .and_then(move |existing_networks| {
                        if existing_networks.is_empty() {
                            let fut = client_copy
                                .network_api()
                                .network_create(NetworkConfig::new(id))
                                .map(|_| ());
                            future::Either::A(fut)
                        } else {
                            future::Either::B(future::ok(()))
                        }
                    })
                    .map_err(|err| {
                        let e = Error::from_docker_error(
                            err,
                            ErrorKind::RuntimeOperation(RuntimeOperation::Init),
                        );
                        log_failure(Level::Warn, &e);
                        e
                    });
                future::Either::A(fut)
            },
        );
        let created = created.then(|result| {
            match result {
                Ok(()) => info!("Successfully initialized module runtime"),
                Err(ref err) => log_failure(Level::Warn, err),
            }

            result
        });

        Box::new(created)
    }

    fn create(&self, module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
        info!("Creating module {}...", module.name());

        // we only want "docker" modules
        if module.type_() != DOCKER_MODULE_TYPE {
            return Box::new(future::err(Error::from(ErrorKind::InvalidModuleType(
                module.type_().to_string(),
            ))));
        }

        let result = module
            .config()
            .clone_create_options()
            .and_then(|create_options| {
                // merge environment variables
                let merged_env = DockerModuleRuntime::merge_env(create_options.env(), module.env());

                let mut labels = create_options
                    .labels()
                    .cloned()
                    .unwrap_or_else(HashMap::new);
                labels.insert(LABEL_KEY.to_string(), LABEL_VALUE.to_string());

                debug!(
                    "Creating container {} with image {}",
                    module.name(),
                    module.config().image()
                );

                let create_options = create_options
                    .with_image(module.config().image().to_string())
                    .with_env(merged_env)
                    .with_labels(labels);

                // Here we don't add the container to the iot edge docker network as the edge-agent is expected to do that.
                // It contains the logic to add a container to the iot edge network only if a network is not already specified.

                Ok(self
                    .client
                    .container_api()
                    .container_create(create_options, module.name())
                    .then(|result| match result {
                        Ok(_) => Ok(module),
                        Err(err) => Err(Error::from_docker_error(
                            err,
                            ErrorKind::RuntimeOperation(RuntimeOperation::CreateModule(
                                module.name().to_string(),
                            )),
                        )),
                    }))
            })
            .into_future()
            .flatten()
            .then(|result| match result {
                Ok(module) => {
                    info!("Successfully created module {}", module.name());
                    Ok(())
                }
                Err(err) => {
                    log_failure(Level::Warn, &err);
                    Err(err)
                }
            });

        Box::new(result)
    }

    fn get(&self, id: &str) -> Self::GetFuture {
        debug!("Getting module {}...", id);

        let id = id.to_string();

        if let Err(err) = ensure_not_empty_with_context(&id, || {
            ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(id.clone()))
        }) {
            return Box::new(future::err(Error::from(err)));
        }

        let client_copy = self.client.clone();

        Box::new(
            self.client
                .container_api()
                .container_inspect(&id, false)
                .then(|result| match result {
                    Ok(container) => {
                        let name =
                            parse_get_response::<Deserializer>(&container).with_context(|_| {
                                ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(id.clone()))
                            })?;
                        let config =
                            DockerConfig::new(name.clone(), ContainerCreateBody::new(), None)
                                .with_context(|_| {
                                    ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(
                                        id.clone(),
                                    ))
                                })?;
                        let module =
                            DockerModule::new(client_copy, name, config).with_context(|_| {
                                ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(id.clone()))
                            })?;
                        let state = runtime_state(container.id(), container.state());
                        Ok((module, state))
                    }
                    Err(err) => {
                        let err = Error::from_docker_error(
                            err,
                            ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(id)),
                        );
                        log_failure(Level::Warn, &err);
                        Err(err)
                    }
                }),
        )
    }

    fn start(&self, id: &str) -> Self::StartFuture {
        info!("Starting module {}...", id);

        let id = id.to_string();

        if let Err(err) = ensure_not_empty_with_context(&id, || {
            ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(id.clone()))
        }) {
            return Box::new(future::err(Error::from(err)));
        }

        Box::new(
            self.client
                .container_api()
                .container_start(&id, "")
                .then(|result| match result {
                    Ok(_) => {
                        info!("Successfully started module {}", id);
                        Ok(())
                    }
                    Err(err) => {
                        let err = Error::from_docker_error(
                            err,
                            ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(id)),
                        );
                        log_failure(Level::Warn, &err);
                        Err(err)
                    }
                }),
        )
    }

    fn stop(&self, id: &str, wait_before_kill: Option<Duration>) -> Self::StopFuture {
        info!("Stopping module {}...", id);

        let id = id.to_string();

        if let Err(err) = ensure_not_empty_with_context(&id, || {
            ErrorKind::RuntimeOperation(RuntimeOperation::StopModule(id.clone()))
        }) {
            return Box::new(future::err(Error::from(err)));
        }

        #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
        Box::new(
            self.client
                .container_api()
                .container_stop(
                    &id,
                    wait_before_kill.map_or(WAIT_BEFORE_KILL_SECONDS, |s| match s.as_secs() {
                        s if s > i32::max_value() as u64 => i32::max_value(),
                        s => s as i32,
                    }),
                )
                .then(|result| match result {
                    Ok(_) => {
                        info!("Successfully stopped module {}", id);
                        Ok(())
                    }
                    Err(err) => {
                        let err = Error::from_docker_error(
                            err,
                            ErrorKind::RuntimeOperation(RuntimeOperation::StopModule(id)),
                        );
                        log_failure(Level::Warn, &err);
                        Err(err)
                    }
                }),
        )
    }

    fn system_info(&self) -> Self::SystemInfoFuture {
        info!("Querying system info...");

        Box::new(
            self.client
                .system_api()
                .system_info()
                .then(|result| match result {
                    Ok(system_info) => {
                        let system_info = CoreSystemInfo::new(
                            system_info
                                .os_type()
                                .unwrap_or(&String::from("Unknown"))
                                .to_string(),
                            system_info
                                .architecture()
                                .unwrap_or(&String::from("Unknown"))
                                .to_string(),
                        );
                        info!("Successfully queried system info");
                        Ok(system_info)
                    }
                    Err(err) => {
                        let err = Error::from_docker_error(
                            err,
                            ErrorKind::RuntimeOperation(RuntimeOperation::SystemInfo),
                        );
                        log_failure(Level::Warn, &err);
                        Err(err)
                    }
                }),
        )
    }

    fn restart(&self, id: &str) -> Self::RestartFuture {
        info!("Restarting module {}...", id);

        let id = id.to_string();

        if let Err(err) = ensure_not_empty_with_context(&id, || {
            ErrorKind::RuntimeOperation(RuntimeOperation::RestartModule(id.clone()))
        }) {
            return Box::new(future::err(Error::from(err)));
        }

        Box::new(
            self.client
                .container_api()
                .container_restart(&id, WAIT_BEFORE_KILL_SECONDS)
                .then(|result| match result {
                    Ok(_) => {
                        info!("Successfully restarted module {}", id);
                        Ok(())
                    }
                    Err(err) => {
                        let err = Error::from_docker_error(
                            err,
                            ErrorKind::RuntimeOperation(RuntimeOperation::RestartModule(id)),
                        );
                        log_failure(Level::Warn, &err);
                        Err(err)
                    }
                }),
        )
    }

    fn remove(&self, id: &str) -> Self::RemoveFuture {
        info!("Removing module {}...", id);

        let id = id.to_string();

        if let Err(err) = ensure_not_empty_with_context(&id, || {
            ErrorKind::RuntimeOperation(RuntimeOperation::RemoveModule(id.clone()))
        }) {
            return Box::new(future::err(Error::from(err)));
        }

        Box::new(
            self.client
                .container_api()
                .container_delete(
                    &id, /* remove volumes */ false, /* force */ true,
                    /* remove link */ false,
                )
                .then(|result| match result {
                    Ok(_) => {
                        info!("Successfully removed module {}", id);
                        Ok(())
                    }
                    Err(err) => {
                        let err = Error::from_docker_error(
                            err,
                            ErrorKind::RuntimeOperation(RuntimeOperation::RemoveModule(id)),
                        );
                        log_failure(Level::Warn, &err);
                        Err(err)
                    }
                }),
        )
    }

    fn list(&self) -> Self::ListFuture {
        debug!("Listing modules...");

        let mut filters = HashMap::new();
        filters.insert("label", LABELS.deref());

        let client_copy = self.client.clone();

        let result = serde_json::to_string(&filters)
            .context(ErrorKind::RuntimeOperation(RuntimeOperation::ListModules))
            .map_err(Error::from)
            .map(|filters| {
                self.client
                    .container_api()
                    .container_list(true, 0, false, &filters)
                    .map(move |containers| {
                        containers
                            .iter()
                            .flat_map(|container| {
                                DockerConfig::new(
                                    container.image().to_string(),
                                    ContainerCreateBody::new()
                                        .with_labels(container.labels().clone()),
                                    None,
                                )
                                .map(|config| {
                                    (
                                        container,
                                        config.with_image_id(container.image_id().clone()),
                                    )
                                })
                            })
                            .flat_map(|(container, config)| {
                                DockerModule::new(
                                    client_copy.clone(),
                                    container
                                        .names()
                                        .iter()
                                        .next()
                                        .map_or("Unknown", |s| &s[1..])
                                        .to_string(),
                                    config,
                                )
                            })
                            .collect()
                    })
                    .map_err(|err| {
                        Error::from_docker_error(
                            err,
                            ErrorKind::RuntimeOperation(RuntimeOperation::ListModules),
                        )
                    })
            })
            .into_future()
            .flatten()
            .then(|result| {
                match result {
                    Ok(_) => debug!("Successfully listed modules"),
                    Err(ref err) => log_failure(Level::Warn, err),
                }

                result
            });
        Box::new(result)
    }

    fn list_with_details(&self) -> Self::ListWithDetailsStream {
        list_with_details(self)
    }

    fn logs(&self, id: &str, options: &LogOptions) -> Self::LogsFuture {
        info!("Getting logs for module {}...", id);

        let id = id.to_string();

        let tail = &options.tail().to_string();
        let result = self
            .client
            .container_api()
            .container_logs(&id, options.follow(), true, true, 0, false, tail)
            .then(|result| match result {
                Ok(logs) => {
                    info!("Successfully got logs for module {}", id);
                    Ok(Logs(id, logs))
                }
                Err(err) => {
                    let err = Error::from_docker_error(
                        err,
                        ErrorKind::RuntimeOperation(RuntimeOperation::GetModuleLogs(id)),
                    );
                    log_failure(Level::Warn, &err);
                    Err(err)
                }
            });
        Box::new(result)
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        self
    }

    fn remove_all(&self) -> Self::RemoveAllFuture {
        let self_for_remove = self.clone();
        Box::new(self.list().and_then(move |list| {
            let n = list.into_iter().map(move |c| {
                <DockerModuleRuntime as ModuleRuntime>::remove(&self_for_remove, c.name())
            });
            future::join_all(n).map(|_| ())
        }))
    }

    fn top(&self, id: &str) -> Self::TopFuture {
        let id = id.to_string();
        Box::new(
            self.client
                .container_api()
                .container_top(&id, "")
                .then(|result| match result {
                    Ok(resp) => {
                        let p = parse_top_response::<Deserializer>(&resp).with_context(|_| {
                            ErrorKind::RuntimeOperation(RuntimeOperation::TopModule(id.clone()))
                        })?;
                        Ok(ModuleTop::new(id, p))
                    }
                    Err(err) => {
                        let err = Error::from_docker_error(
                            err,
                            ErrorKind::RuntimeOperation(RuntimeOperation::TopModule(id)),
                        );
                        Err(err)
                    }
                }),
        )
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

/// Invokes `ModuleRuntime::list`, then `Module::runtime_state` on each Module.
/// Modules whose `runtime_state` returns `NotFound` are filtered out from the result,
/// instead of letting the whole `list_with_details` call fail.
fn list_with_details<MR, M>(
    runtime: &MR,
) -> Box<Stream<Item = (M, ModuleRuntimeState), Error = Error> + Send>
where
    MR: ModuleRuntime<Error = Error, Config = <M as Module>::Config, Module = M>,
    <MR as ModuleRuntime>::ListFuture: 'static,
    M: Module<Error = Error> + Send + 'static,
    <M as Module>::Config: Send,
{
    Box::new(
        runtime
            .list()
            .into_stream()
            .map(|list| {
                stream::futures_unordered(
                    list.into_iter()
                        .map(|module| module.runtime_state().map(|state| (module, state))),
                )
            })
            .flatten()
            .then(Ok::<_, Error>) // Ok(_) -> Ok(Ok(_)), Err(_) -> Ok(Err(_)), ! -> Err(_)
            .filter_map(|value| match value {
                Ok(value) => Some(Ok(value)),
                Err(err) => match err.kind() {
                    ErrorKind::NotFound(_) => None,
                    _ => Some(Err(err)),
                },
            })
            .then(Result::unwrap), // Ok(Ok(_)) -> Ok(_), Ok(Err(_)) -> Err(_), Err(_) -> !
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    use futures::future::FutureResult;
    use futures::stream::Empty;
    #[cfg(unix)]
    use tempfile::NamedTempFile;
    use tokio;
    use url::Url;

    use docker::models::ContainerCreateBody;
    use edgelet_core::pid::Pid;
    use edgelet_core::ModuleRegistry;

    use error::{Error, ErrorKind};

    #[test]
    #[should_panic(expected = "URL does not have a recognized scheme")]
    fn invalid_uri_prefix_fails() {
        let _mri =
            DockerModuleRuntime::new(&Url::parse("foo:///this/is/not/valid").unwrap()).unwrap();
    }

    #[cfg(unix)]
    #[test]
    #[should_panic(expected = "Socket file could not be found")]
    fn invalid_uds_path_fails() {
        let _mri =
            DockerModuleRuntime::new(&Url::parse("unix:///this/file/does/not/exist").unwrap())
                .unwrap();
    }

    #[cfg(unix)]
    #[test]
    fn create_with_uds_succeeds() {
        let file = NamedTempFile::new().unwrap();
        let file_path = file.path().to_str().unwrap();
        let _mri = DockerModuleRuntime::new(&Url::parse(&format!("unix://{}", file_path)).unwrap())
            .unwrap();
    }

    #[test]
    fn image_remove_with_empty_name_fails() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "";

        let task = ModuleRegistry::remove(&mri, name).then(|res| match res {
            Ok(_) => Err("Expected error but got a result.".to_string()),
            Err(err) => match err.kind() {
                ErrorKind::RegistryOperation(RegistryOperation::RemoveImage(s)) if s == name => {
                    Ok(())
                }
                kind => panic!(
                    "Expected `RegistryOperation(RemoveImage)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn image_remove_with_white_space_name_fails() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "     ";

        let task = ModuleRegistry::remove(&mri, name).then(|res| match res {
            Ok(_) => Err("Expected error but got a result.".to_string()),
            Err(err) => match err.kind() {
                ErrorKind::RegistryOperation(RegistryOperation::RemoveImage(s)) if s == name => {
                    Ok(())
                }
                kind => panic!(
                    "Expected `RegistryOperation(RemoveImage)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn merge_env_empty() {
        let cur_env = Some(&[][..]);
        let new_env = HashMap::new();
        assert_eq!(0, DockerModuleRuntime::merge_env(cur_env, &new_env).len());
    }

    #[test]
    fn merge_env_new_empty() {
        let cur_env = Some(vec!["k1=v1".to_string(), "k2=v2".to_string()]);
        let new_env = HashMap::new();
        let mut merged_env =
            DockerModuleRuntime::merge_env(cur_env.as_ref().map(AsRef::as_ref), &new_env);
        merged_env.sort();
        assert_eq!(vec!["k1=v1", "k2=v2"], merged_env);
    }

    #[test]
    fn merge_env_extend_new() {
        let cur_env = Some(vec!["k1=v1".to_string(), "k2=v2".to_string()]);
        let mut new_env = HashMap::new();
        new_env.insert("k3".to_string(), "v3".to_string());
        let mut merged_env =
            DockerModuleRuntime::merge_env(cur_env.as_ref().map(AsRef::as_ref), &new_env);
        merged_env.sort();
        assert_eq!(vec!["k1=v1", "k2=v2", "k3=v3"], merged_env);
    }

    #[test]
    fn merge_env_extend_replace_new() {
        let cur_env = Some(vec!["k1=v1".to_string(), "k2=v2".to_string()]);
        let mut new_env = HashMap::new();
        new_env.insert("k2".to_string(), "v02".to_string());
        new_env.insert("k3".to_string(), "v3".to_string());
        let mut merged_env =
            DockerModuleRuntime::merge_env(cur_env.as_ref().map(AsRef::as_ref), &new_env);
        merged_env.sort();
        assert_eq!(vec!["k1=v1", "k2=v2", "k3=v3"], merged_env);
    }

    #[test]
    fn create_fails_for_non_docker_type() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "not_docker".to_string();

        let module_config = ModuleSpec::new(
            "m1".to_string(),
            name.clone(),
            DockerConfig::new("nginx:latest".to_string(), ContainerCreateBody::new(), None)
                .unwrap(),
            HashMap::new(),
        )
        .unwrap();

        let task = mri.create(module_config).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::InvalidModuleType(s) if s == &name => Ok::<_, Error>(()),
                kind => panic!("Expected `InvalidModuleType` error but got {:?}.", kind),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn start_fails_for_empty_id() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "";

        let task = mri.start(name).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(StartModule)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn start_fails_for_white_space_id() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "      ";

        let task = mri.start(name).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(StartModule)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn stop_fails_for_empty_id() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "";

        let task = mri.stop(name, None).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::StopModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(StopModule)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn stop_fails_for_white_space_id() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "     ";

        let task = mri.stop(name, None).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::StopModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(StopModule)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn restart_fails_for_empty_id() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "";

        let task = mri.restart(name).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::RestartModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(RestartModule)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn restart_fails_for_white_space_id() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "     ";

        let task = mri.restart(name).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::RestartModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(RestartModule)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn remove_fails_for_empty_id() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "";

        let task = ModuleRuntime::remove(&mri, name).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::RemoveModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(RemoveModule)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn remove_fails_for_white_space_id() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "    ";

        let task = ModuleRuntime::remove(&mri, name).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::RemoveModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(RemoveModule)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn get_fails_for_empty_id() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "";

        let task = ModuleRuntime::get(&mri, name).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(GetModule)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn get_fails_for_white_space_id() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "    ";

        let task = ModuleRuntime::get(&mri, name).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(GetModule)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn list_with_details_filters_out_deleted_containers() {
        let runtime = TestModuleList {
            modules: vec![
                TestModule {
                    name: "a".to_string(),
                    runtime_state_behavior: TestModuleRuntimeStateBehavior::Default,
                },
                TestModule {
                    name: "b".to_string(),
                    runtime_state_behavior: TestModuleRuntimeStateBehavior::NotFound,
                },
                TestModule {
                    name: "c".to_string(),
                    runtime_state_behavior: TestModuleRuntimeStateBehavior::NotFound,
                },
                TestModule {
                    name: "d".to_string(),
                    runtime_state_behavior: TestModuleRuntimeStateBehavior::Default,
                },
            ],
        };

        assert_eq!(
            runtime.list_with_details().collect().wait().unwrap(),
            vec![
                (
                    TestModule {
                        name: "a".to_string(),
                        runtime_state_behavior: TestModuleRuntimeStateBehavior::Default,
                    },
                    ModuleRuntimeState::default().with_pid(Pid::Any)
                ),
                (
                    TestModule {
                        name: "d".to_string(),
                        runtime_state_behavior: TestModuleRuntimeStateBehavior::Default,
                    },
                    ModuleRuntimeState::default().with_pid(Pid::Any)
                ),
            ]
        );
    }

    #[test]
    fn top_fails_for_empty_id() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "";

        let task = ModuleRuntime::top(&mri, name).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::TopModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(TopModule)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn top_fails_for_white_space_id() {
        let mri = DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap()).unwrap();
        let name = "    ";

        let task = ModuleRuntime::top(&mri, name).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                ErrorKind::RuntimeOperation(RuntimeOperation::TopModule(s)) if s == name => {
                    Ok::<_, Error>(())
                }
                kind => panic!(
                    "Expected `RuntimeOperation(TopModule)` error but got {:?}.",
                    kind
                ),
            },
        });

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(task)
            .unwrap();
    }

    #[test]
    fn parse_get_response_returns_the_name() {
        let response = InlineResponse200::new().with_name("hello".to_string());
        let name = parse_get_response::<Deserializer>(&response);
        assert!(name.is_ok());
        assert_eq!("hello".to_string(), name.unwrap());
    }

    #[test]
    fn parse_get_response_returns_error_when_name_is_missing() {
        let response = InlineResponse200::new();
        let name = parse_get_response::<Deserializer>(&response);
        assert!(name.is_err());
        assert_eq!("missing field `Name`", format!("{}", name.unwrap_err()));
    }

    #[test]
    fn parse_top_response_returns_pid_array() {
        let response = InlineResponse2001::new()
            .with_titles(vec!["PID".to_string()])
            .with_processes(vec![vec!["123".to_string()]]);
        let pids = parse_top_response::<Deserializer>(&response);
        assert!(pids.is_ok());
        assert_eq!(vec![Pid::Value(123)], pids.unwrap());
    }

    #[test]
    fn parse_top_response_returns_error_when_titles_is_missing() {
        let response = InlineResponse2001::new().with_processes(vec![vec!["123".to_string()]]);
        let pids = parse_top_response::<Deserializer>(&response);
        assert!(pids.is_err());
        assert_eq!("missing field `Titles`", format!("{}", pids.unwrap_err()));
    }

    #[test]
    fn parse_top_response_returns_error_when_pid_title_is_missing() {
        let response = InlineResponse2001::new().with_titles(vec!["Command".to_string()]);
        let pids = parse_top_response::<Deserializer>(&response);
        assert!(pids.is_err());
        assert_eq!(
            "invalid value: sequence, expected array including the column title \'PID\'",
            format!("{}", pids.unwrap_err())
        );
    }

    #[test]
    fn parse_top_response_returns_error_when_processes_is_missing() {
        let response = InlineResponse2001::new().with_titles(vec!["PID".to_string()]);
        let pids = parse_top_response::<Deserializer>(&response);
        assert!(pids.is_err());
        assert_eq!(
            "missing field `Processes`",
            format!("{}", pids.unwrap_err())
        );
    }

    #[test]
    fn parse_top_response_returns_error_when_process_pid_is_missing() {
        let response = InlineResponse2001::new()
            .with_titles(vec!["Command".to_string(), "PID".to_string()])
            .with_processes(vec![vec!["sh".to_string()]]);
        let pids = parse_top_response::<Deserializer>(&response);
        assert!(pids.is_err());
        assert_eq!(
            "invalid length 1, expected at least 2 columns",
            format!("{}", pids.unwrap_err())
        );
    }

    #[test]
    fn parse_top_response_returns_error_when_process_pid_is_not_i32() {
        let response = InlineResponse2001::new()
            .with_titles(vec!["PID".to_string()])
            .with_processes(vec![vec!["xyz".to_string()]]);
        let pids = parse_top_response::<Deserializer>(&response);
        assert!(pids.is_err());
        assert_eq!(
            "invalid value: string \"xyz\", expected a process ID number",
            format!("{}", pids.unwrap_err())
        );
    }

    struct TestConfig;

    #[derive(Clone, Copy, Debug, PartialEq)]
    enum TestModuleRuntimeStateBehavior {
        Default,
        NotFound,
    }

    #[derive(Clone, Debug, PartialEq)]
    struct TestModule {
        name: String,
        runtime_state_behavior: TestModuleRuntimeStateBehavior,
    }

    impl Module for TestModule {
        type Config = TestConfig;
        type Error = Error;
        type RuntimeStateFuture = FutureResult<ModuleRuntimeState, Self::Error>;

        fn name(&self) -> &str {
            &self.name
        }

        fn type_(&self) -> &str {
            ""
        }

        fn config(&self) -> &Self::Config {
            &TestConfig
        }

        fn runtime_state(&self) -> Self::RuntimeStateFuture {
            match self.runtime_state_behavior {
                TestModuleRuntimeStateBehavior::Default => {
                    future::ok(ModuleRuntimeState::default().with_pid(Pid::Any))
                }
                TestModuleRuntimeStateBehavior::NotFound => {
                    future::err(ErrorKind::NotFound(String::new()).into())
                }
            }
        }
    }

    #[derive(Clone)]
    struct TestModuleList {
        modules: Vec<TestModule>,
    }

    impl ModuleRegistry for TestModuleList {
        type Config = TestConfig;
        type Error = Error;
        type PullFuture = FutureResult<(), Self::Error>;
        type RemoveFuture = FutureResult<(), Self::Error>;

        fn pull(&self, _config: &Self::Config) -> Self::PullFuture {
            unimplemented!()
        }

        fn remove(&self, _name: &str) -> Self::RemoveFuture {
            unimplemented!()
        }
    }

    impl ModuleRuntime for TestModuleList {
        type Error = Error;
        type Config = TestConfig;
        type Module = TestModule;
        type ModuleRegistry = Self;
        type Chunk = String;
        type Logs = Empty<Self::Chunk, Self::Error>;

        type CreateFuture = FutureResult<(), Self::Error>;
        type GetFuture = FutureResult<(Self::Module, ModuleRuntimeState), Self::Error>;
        type InitFuture = FutureResult<(), Self::Error>;
        type ListFuture = FutureResult<Vec<Self::Module>, Self::Error>;
        type ListWithDetailsStream =
            Box<Stream<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
        type LogsFuture = FutureResult<Self::Logs, Self::Error>;
        type RemoveFuture = FutureResult<(), Self::Error>;
        type RestartFuture = FutureResult<(), Self::Error>;
        type StartFuture = FutureResult<(), Self::Error>;
        type StopFuture = FutureResult<(), Self::Error>;
        type SystemInfoFuture = FutureResult<CoreSystemInfo, Self::Error>;
        type RemoveAllFuture = FutureResult<(), Self::Error>;
        type TopFuture = FutureResult<ModuleTop, Self::Error>;

        fn init(&self) -> Self::InitFuture {
            unimplemented!()
        }

        fn create(&self, _module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
            unimplemented!()
        }

        fn get(&self, _id: &str) -> Self::GetFuture {
            unimplemented!()
        }

        fn start(&self, _id: &str) -> Self::StartFuture {
            unimplemented!()
        }

        fn stop(&self, _id: &str, _wait_before_kill: Option<Duration>) -> Self::StopFuture {
            unimplemented!()
        }

        fn system_info(&self) -> Self::SystemInfoFuture {
            unimplemented!()
        }

        fn restart(&self, _id: &str) -> Self::RestartFuture {
            unimplemented!()
        }

        fn remove(&self, _id: &str) -> Self::RemoveFuture {
            unimplemented!()
        }

        fn list(&self) -> Self::ListFuture {
            future::ok(self.modules.clone())
        }

        fn list_with_details(&self) -> Self::ListWithDetailsStream {
            list_with_details(self)
        }

        fn logs(&self, _id: &str, _options: &LogOptions) -> Self::LogsFuture {
            unimplemented!()
        }

        fn registry(&self) -> &Self::ModuleRegistry {
            self
        }

        fn remove_all(&self) -> Self::RemoveAllFuture {
            unimplemented!()
        }

        fn top(&self, _id: &str) -> Self::TopFuture {
            unimplemented!()
        }
    }
}
