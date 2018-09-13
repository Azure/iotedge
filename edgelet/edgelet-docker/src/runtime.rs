// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::convert::From;
use std::ops::Deref;
use std::time::Duration;

use base64;
use futures::future;
use futures::prelude::*;
use hyper::{Body, Chunk as HyperChunk, Client};
use log::Level;
use serde_json;
use tokio_core::reactor::Handle;
use url::Url;

use client::DockerClient;
use config::DockerConfig;
use docker::apis::client::APIClient;
use docker::apis::configuration::Configuration;
use docker::models::{ContainerCreateBody, NetworkConfig};
use edgelet_core::{
    LogOptions, Module, ModuleRegistry, ModuleRuntime, ModuleSpec, SystemInfo as CoreSystemInfo,
};
use edgelet_http::UrlConnector;
use edgelet_utils::log_failure;

use error::{Error, Result};
use module::{DockerModule, MODULE_TYPE as DOCKER_MODULE_TYPE};

const WAIT_BEFORE_KILL_SECONDS: i32 = 10;

static LABEL_KEY: &str = "net.azure-devices.edge.owner";
static LABEL_VALUE: &str = "Microsoft.Azure.Devices.Edge.Agent";

lazy_static! {
    static ref LABELS: Vec<&'static str> = {
        let mut labels = Vec::new();
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
    pub fn new(docker_url: &Url, handle: &Handle) -> Result<DockerModuleRuntime> {
        // build the hyper client
        let client = Client::configure()
            .connector(UrlConnector::new(docker_url, handle)?)
            .build(handle);

        // extract base path - the bit that comes after the scheme
        let base_path = get_base_path(docker_url);
        let mut configuration = Configuration::new(client);
        configuration.base_path = base_path.to_string();

        let scheme = docker_url.scheme().to_string();
        configuration.uri_composer = Box::new(move |base_path, path| {
            Ok(UrlConnector::build_hyper_uri(&scheme, base_path, path)?)
        });

        Ok(DockerModuleRuntime {
            client: DockerClient::new(APIClient::new(configuration)),
            network_id: None,
        })
    }

    pub fn with_network_id(mut self, network_id: String) -> DockerModuleRuntime {
        self.network_id = Some(network_id);
        self
    }

    fn merge_env(cur_env: Option<&Vec<String>>, new_env: &HashMap<String, String>) -> Vec<String> {
        // build a new merged hashmap containing string slices for keys and values
        // pointing into String instances in new_env
        let mut merged_env = HashMap::new();
        merged_env.extend(new_env.iter().map(|(k, v)| (k.as_str(), v.as_str())));

        if let Some(env) = cur_env {
            // extend merged_env with variables in cur_env (again, these are
            // only string slices pointing into strings inside cur_env)
            merged_env.extend(env.iter().filter_map(|s| {
                let mut tokens = s.splitn(2, '=');
                tokens.nth(0).map(|key| (key, tokens.nth(0).unwrap_or("")))
            }));
        }

        // finally build a new Vec<String>; we alloc new strings here
        merged_env
            .iter()
            .map(|(key, value)| format!("{}={}", key, value))
            .collect()
    }
}

fn get_base_path(url: &Url) -> &str {
    match url.scheme() {
        "unix" => url.path(),
        _ => url.as_str(),
    }
}

impl ModuleRegistry for DockerModuleRuntime {
    type Error = Error;
    type PullFuture = Box<Future<Item = (), Error = Self::Error>>;
    type RemoveFuture = Box<Future<Item = (), Error = Self::Error>>;
    type Config = DockerConfig;

    fn pull(&self, config: &Self::Config) -> Self::PullFuture {
        let response = config
            .auth()
            .map(|a| serde_json::to_string(a).map(|json| base64::encode(&json)))
            .unwrap_or_else(|| Ok("".to_string()))
            .map(|creds: String| {
                debug!("Pulling {}", config.image());
                let ok = self
                    .client
                    .image_api()
                    .image_create(config.image(), "", "", "", "", &creds, "")
                    .map_err(|err| {
                        let e = Error::from(err);
                        log_failure(Level::Warn, &e);
                        e
                    });
                future::Either::A(ok)
            }).unwrap_or_else(|e| future::Either::B(future::err(Error::from(e))));
        Box::new(response)
    }

    fn remove(&self, name: &str) -> Self::RemoveFuture {
        debug!("Removing {}", name);
        Box::new(
            self.client
                .image_api()
                .image_delete(fensure_not_empty!(name), false, false)
                .map(|_| ())
                .map_err(|err| {
                    let e = Error::from(err);
                    log_failure(Level::Warn, &e);
                    e
                }),
        )
    }
}

impl ModuleRuntime for DockerModuleRuntime {
    type Error = Error;
    type Config = DockerConfig;
    type Module = DockerModule<UrlConnector>;
    type ModuleRegistry = Self;
    type Chunk = Chunk;
    type Logs = Logs;

    type CreateFuture = Box<Future<Item = (), Error = Self::Error>>;
    type InitFuture = Box<Future<Item = (), Error = Self::Error>>;
    type ListFuture = Box<Future<Item = Vec<Self::Module>, Error = Self::Error>>;
    type LogsFuture = Box<Future<Item = Self::Logs, Error = Self::Error>>;
    type RemoveFuture = Box<Future<Item = (), Error = Self::Error>>;
    type RestartFuture = Box<Future<Item = (), Error = Self::Error>>;
    type StartFuture = Box<Future<Item = (), Error = Self::Error>>;
    type StopFuture = Box<Future<Item = (), Error = Self::Error>>;
    type SystemInfoFuture = Box<Future<Item = CoreSystemInfo, Error = Self::Error>>;
    type RemoveAllFuture = Box<Future<Item = (), Error = Self::Error>>;

    fn init(&self) -> Self::InitFuture {
        let created = self
            .network_id
            .as_ref()
            .map(|id| {
                let id = id.clone();
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
                    }).map_err(|err| {
                        let e = Error::from(err);
                        log_failure(Level::Warn, &e);
                        e
                    });
                future::Either::A(fut)
            }).unwrap_or_else(|| future::Either::B(future::ok(())));

        Box::new(created)
    }

    fn create(&self, module: ModuleSpec<Self::Config>) -> Self::CreateFuture {
        // we only want "docker" modules
        fensure!(module.type_(), module.type_() == DOCKER_MODULE_TYPE);

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
                    .map_err(Error::from)
                    .map(|_| ()))
            });

        match result {
            Ok(f) => Box::new(f),
            Err(err) => {
                log_failure(Level::Warn, &err);
                Box::new(future::err(err))
            }
        }
    }

    fn start(&self, id: &str) -> Self::StartFuture {
        debug!("Starting container {}", id);
        Box::new(
            self.client
                .container_api()
                .container_start(fensure_not_empty!(id), "")
                .map_err(|err| {
                    let e = Error::from(err);
                    log_failure(Level::Warn, &e);
                    e
                }).map(|_| ()),
        )
    }

    fn stop(&self, id: &str, wait_before_kill: Option<Duration>) -> Self::StopFuture {
        debug!("Stopping container {}", id);
        Box::new(
            self.client
                .container_api()
                .container_stop(
                    fensure_not_empty!(id),
                    wait_before_kill
                        .map(|s| s.as_secs() as i32)
                        .unwrap_or(WAIT_BEFORE_KILL_SECONDS),
                ).map_err(|err| {
                    let e = Error::from(err);
                    log_failure(Level::Warn, &e);
                    e
                }).map(|_| ()),
        )
    }

    fn system_info(&self) -> Self::SystemInfoFuture {
        Box::new(
            self.client
                .system_api()
                .system_info()
                .map(|system_info| {
                    CoreSystemInfo::new(
                        system_info
                            .os_type()
                            .unwrap_or(&String::from("Unknown"))
                            .to_string(),
                        system_info
                            .architecture()
                            .unwrap_or(&String::from("Unknown"))
                            .to_string(),
                    )
                }).map_err(|err| {
                    let e = Error::from(err);
                    log_failure(Level::Warn, &e);
                    e
                }),
        )
    }

    fn restart(&self, id: &str) -> Self::RestartFuture {
        debug!("Restarting container {}", id);
        Box::new(
            self.client
                .container_api()
                .container_restart(fensure_not_empty!(id), WAIT_BEFORE_KILL_SECONDS)
                .map_err(|err| {
                    let e = Error::from(err);
                    log_failure(Level::Warn, &e);
                    e
                }).map(|_| ()),
        )
    }

    fn remove(&self, id: &str) -> Self::RemoveFuture {
        debug!("Removing container {}", id);
        Box::new(
            self.client
                .container_api()
                .container_delete(
                    fensure_not_empty!(id),
                    /* remove volumes */ false,
                    /* force */ true,
                    /* remove link */ false,
                ).map_err(|err| {
                    let e = Error::from(err);
                    log_failure(Level::Warn, &e);
                    e
                }).map(|_| ()),
        )
    }

    fn list(&self) -> Self::ListFuture {
        let mut filters = HashMap::new();
        filters.insert("label", LABELS.deref());

        let client_copy = self.client.clone();

        let result = serde_json::to_string(&filters)
            .and_then(|filters| {
                Ok(self
                    .client
                    .container_api()
                    .container_list(true, 0, true, &filters)
                    .map(move |containers| {
                        containers
                            .iter()
                            .flat_map(|container| {
                                DockerConfig::new(
                                    container.image(),
                                    ContainerCreateBody::new()
                                        .with_labels(container.labels().clone()),
                                    None,
                                ).map(|config| {
                                    (
                                        container,
                                        config.with_image_id(container.image_id().clone()),
                                    )
                                })
                            }).flat_map(|(container, config)| {
                                DockerModule::new(
                                    client_copy.clone(),
                                    container
                                        .names()
                                        .iter()
                                        .nth(0)
                                        .map(|s| &s[1..])
                                        .unwrap_or("Unknown"),
                                    config,
                                )
                            }).collect()
                    }).map_err(Error::from))
            }).map_err(Error::from);

        match result {
            Ok(f) => Box::new(f),
            Err(err) => {
                log_failure(Level::Warn, &err);
                Box::new(future::err(err))
            }
        }
    }

    fn logs(&self, id: &str, options: &LogOptions) -> Self::LogsFuture {
        let tail = &options.tail().to_string();
        let result = self
            .client
            .container_api()
            .container_logs(id, options.follow(), true, true, 0, false, tail)
            .map(Logs)
            .map_err(|err| {
                let e = Error::from(err);
                log_failure(Level::Warn, &e);
                e
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
}

#[derive(Debug)]
pub struct Logs(Body);

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

impl Stream for Logs {
    type Item = Chunk;
    type Error = Error;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        if let Some(c) = try_ready!(self.0.poll()) {
            Ok(Async::Ready(Some(Chunk(c))))
        } else {
            Ok(Async::Ready(None))
        }
    }
}

impl Into<Body> for Logs {
    fn into(self) -> Body {
        self.0
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
    use std::mem;

    #[cfg(unix)]
    use tempfile::NamedTempFile;
    use tokio_core::reactor::Core;
    use url::Url;

    use docker::models::ContainerCreateBody;
    use edgelet_core::ModuleRegistry;

    use error::{Error, ErrorKind};

    #[test]
    #[should_panic(expected = "Invalid uri")]
    fn invalid_uri_prefix_fails() {
        let core = Core::new().unwrap();
        let _mri = DockerModuleRuntime::new(
            &Url::parse("foo:///this/is/not/valid").unwrap(),
            &core.handle(),
        ).unwrap();
    }

    #[cfg(unix)]
    #[test]
    #[should_panic(expected = "Invalid uri")]
    fn invalid_uds_path_fails() {
        let core = Core::new().unwrap();
        let _mri = DockerModuleRuntime::new(
            &Url::parse("unix:///this/file/does/not/exist").unwrap(),
            &core.handle(),
        ).unwrap();
    }

    #[cfg(unix)]
    #[test]
    fn create_with_uds_succeeds() {
        let core = Core::new().unwrap();
        let file = NamedTempFile::new().unwrap();
        let file_path = file.path().to_str().unwrap();
        let _mri = DockerModuleRuntime::new(
            &Url::parse(&format!("unix://{}", file_path)).unwrap(),
            &core.handle(),
        ).unwrap();
    }

    fn empty_test<F, R>(tester: F)
    where
        F: Fn(&mut DockerModuleRuntime) -> R,
        R: Future<Item = (), Error = Error>,
    {
        let mut core = Core::new().unwrap();
        let mut mri =
            DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap(), &core.handle())
                .unwrap();

        let task = tester(&mut mri).then(|res| match res {
            Ok(_) => Err("Expected error but got a result.".to_string()),
            Err(err) => {
                if mem::discriminant(err.kind()) == mem::discriminant(&ErrorKind::Utils) {
                    Ok(())
                } else {
                    Err(format!(
                        "Wrong error kind. Expected `ArgumentEmpty` found {:?}",
                        err
                    ))
                }
            }
        });

        core.run(task).unwrap();
    }

    #[test]
    fn image_remove_with_empty_name_fails() {
        empty_test(|ref mut mri| <DockerModuleRuntime as ModuleRegistry>::remove(mri, ""));
    }

    #[test]
    fn image_remove_with_white_space_name_fails() {
        empty_test(|ref mut mri| <DockerModuleRuntime as ModuleRegistry>::remove(mri, "     "));
    }

    #[test]
    fn merge_env_empty() {
        let cur_env = Some(vec![]);
        let new_env = HashMap::new();
        assert_eq!(
            0,
            DockerModuleRuntime::merge_env(cur_env.as_ref(), &new_env).len()
        );
    }

    #[test]
    fn merge_env_new_empty() {
        let cur_env = Some(vec!["k1=v1".to_string(), "k2=v2".to_string()]);
        let new_env = HashMap::new();
        let mut merged_env = DockerModuleRuntime::merge_env(cur_env.as_ref(), &new_env);
        merged_env.sort();
        assert_eq!(vec!["k1=v1", "k2=v2"], merged_env);
    }

    #[test]
    fn merge_env_extend_new() {
        let cur_env = Some(vec!["k1=v1".to_string(), "k2=v2".to_string()]);
        let mut new_env = HashMap::new();
        new_env.insert("k3".to_string(), "v3".to_string());
        let mut merged_env = DockerModuleRuntime::merge_env(cur_env.as_ref(), &new_env);
        merged_env.sort();
        assert_eq!(vec!["k1=v1", "k2=v2", "k3=v3"], merged_env);
    }

    #[test]
    fn merge_env_extend_replace_new() {
        let cur_env = Some(vec!["k1=v1".to_string(), "k2=v2".to_string()]);
        let mut new_env = HashMap::new();
        new_env.insert("k2".to_string(), "v02".to_string());
        new_env.insert("k3".to_string(), "v3".to_string());
        let mut merged_env = DockerModuleRuntime::merge_env(cur_env.as_ref(), &new_env);
        merged_env.sort();
        assert_eq!(vec!["k1=v1", "k2=v2", "k3=v3"], merged_env);
    }

    #[test]
    fn create_fails_for_non_docker_type() {
        let mut core = Core::new().unwrap();
        let mri =
            DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap(), &core.handle())
                .unwrap();

        let module_config = ModuleSpec::new(
            "m1",
            "not_docker",
            DockerConfig::new("nginx:latest", ContainerCreateBody::new(), None).unwrap(),
            HashMap::new(),
        ).unwrap();

        let task = mri.create(module_config).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => Ok(()) as Result<()>,
                _ => panic!("Expected utils error. Got some other error."),
            },
        });

        core.run(task).unwrap();
    }

    #[test]
    fn start_fails_for_empty_id() {
        let mut core = Core::new().unwrap();
        let mri =
            DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap(), &core.handle())
                .unwrap();

        let task = mri.start("").then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => Ok(()) as Result<()>,
                _ => panic!("Expected utils error. Got some other error."),
            },
        });

        core.run(task).unwrap();
    }

    #[test]
    fn start_fails_for_white_space_id() {
        let mut core = Core::new().unwrap();
        let mri =
            DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap(), &core.handle())
                .unwrap();

        let task = mri.start("      ").then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => Ok(()) as Result<()>,
                _ => panic!("Expected utils error. Got some other error."),
            },
        });

        core.run(task).unwrap();
    }

    #[test]
    fn stop_fails_for_empty_id() {
        let mut core = Core::new().unwrap();
        let mri =
            DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap(), &core.handle())
                .unwrap();

        let task = mri.stop("", None).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => Ok(()) as Result<()>,
                _ => panic!("Expected utils error. Got some other error."),
            },
        });

        core.run(task).unwrap();
    }

    #[test]
    fn stop_fails_for_white_space_id() {
        let mut core = Core::new().unwrap();
        let mri =
            DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap(), &core.handle())
                .unwrap();

        let task = mri.stop("     ", None).then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => Ok(()) as Result<()>,
                _ => panic!("Expected utils error. Got some other error."),
            },
        });

        core.run(task).unwrap();
    }

    #[test]
    fn restart_fails_for_empty_id() {
        let mut core = Core::new().unwrap();
        let mri =
            DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap(), &core.handle())
                .unwrap();

        let task = mri.restart("").then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => Ok(()) as Result<()>,
                _ => panic!("Expected utils error. Got some other error."),
            },
        });

        core.run(task).unwrap();
    }

    #[test]
    fn restart_fails_for_white_space_id() {
        let mut core = Core::new().unwrap();
        let mri =
            DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap(), &core.handle())
                .unwrap();

        let task = mri.restart("     ").then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => Ok(()) as Result<()>,
                _ => panic!("Expected utils error. Got some other error."),
            },
        });

        core.run(task).unwrap();
    }

    #[test]
    fn remove_fails_for_empty_id() {
        let mut core = Core::new().unwrap();
        let mri =
            DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap(), &core.handle())
                .unwrap();

        let task = ModuleRuntime::remove(&mri, "").then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => Ok(()) as Result<()>,
                _ => panic!("Expected utils error. Got some other error."),
            },
        });

        core.run(task).unwrap();
    }

    #[test]
    fn remove_fails_for_white_space_id() {
        let mut core = Core::new().unwrap();
        let mri =
            DockerModuleRuntime::new(&Url::parse("http://localhost/").unwrap(), &core.handle())
                .unwrap();

        let task = ModuleRuntime::remove(&mri, "    ").then(|result| match result {
            Ok(_) => panic!("Expected test to fail but it didn't!"),
            Err(err) => match err.kind() {
                &ErrorKind::Utils => Ok(()) as Result<()>,
                _ => panic!("Expected utils error. Got some other error."),
            },
        });

        core.run(task).unwrap();
    }
}
