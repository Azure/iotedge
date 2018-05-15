// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::convert::From;
use std::ops::Deref;

use base64;
use futures::future;
use futures::prelude::*;
use hyper::Client;
use serde_json;
use tokio_core::reactor::Handle;
use url::Url;

use client::DockerClient;
use config::DockerConfig;
use docker::apis::client::APIClient;
use docker::apis::configuration::Configuration;
use docker::models::{ContainerCreateBody, ContainerCreateBodyNetworkingConfig, EndpointSettings,
                     NetworkConfig};
use edgelet_core::{ModuleRegistry, ModuleRuntime, ModuleSpec};
use edgelet_http::UrlConnector;
use edgelet_utils::serde_clone;

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
                let ok = self.client
                    .image_api()
                    .image_create(config.image(), "", "", "", "", &creds, "")
                    .map_err(Error::from);
                future::Either::A(ok)
            })
            .unwrap_or_else(|e| future::Either::B(future::err(Error::from(e))));
        Box::new(response)
    }

    fn remove(&self, name: &str) -> Self::RemoveFuture {
        Box::new(
            self.client
                .image_api()
                .image_delete(fensure_not_empty!(name), false, false)
                .map(|_| ())
                .map_err(Error::from),
        )
    }
}

impl ModuleRuntime for DockerModuleRuntime {
    type Error = Error;
    type Config = DockerConfig;
    type Module = DockerModule<UrlConnector>;
    type ModuleRegistry = Self;
    type CreateFuture = Box<Future<Item = (), Error = Self::Error>>;
    type StartFuture = Box<Future<Item = (), Error = Self::Error>>;
    type StopFuture = Box<Future<Item = (), Error = Self::Error>>;
    type RestartFuture = Box<Future<Item = (), Error = Self::Error>>;
    type RemoveFuture = Box<Future<Item = (), Error = Self::Error>>;
    type ListFuture = Box<Future<Item = Vec<Self::Module>, Error = Self::Error>>;
    type InitFuture = Box<Future<Item = (), Error = Self::Error>>;

    fn init(&self) -> Self::InitFuture {
        let created = self.network_id
            .as_ref()
            .map(|id| {
                let id = id.clone();
                let filter = format!(r#"{{"name":{{"{}":true}}}}"#, id);
                let client_copy = self.client.clone();
                let fut = self.client
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
                    .map_err(Error::from);
                future::Either::A(fut)
            })
            .unwrap_or_else(|| future::Either::B(future::ok(())));

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

                let mut create_options = create_options
                    .with_image(module.config().image().to_string())
                    .with_env(merged_env)
                    .with_labels(labels);

                // add the container to the custom network
                if let Some(ref network_id) = self.network_id {
                    let mut network_config = create_options
                        .networking_config()
                        .and_then(|network_config| serde_clone(network_config).ok())
                        .unwrap_or_else(ContainerCreateBodyNetworkingConfig::new);

                    let mut endpoints_config = network_config
                        .endpoints_config()
                        .and_then(|endpoints_config| serde_clone(endpoints_config).ok())
                        .unwrap_or_else(HashMap::new);
                    if !endpoints_config.contains_key(network_id.as_str()) {
                        endpoints_config.insert(network_id.clone(), EndpointSettings::new());
                    }

                    network_config = network_config.with_endpoints_config(endpoints_config);
                    create_options = create_options.with_networking_config(network_config);
                }

                Ok(self.client
                    .container_api()
                    .container_create(create_options, module.name())
                    .map_err(Error::from)
                    .map(|_| ()))
            });

        match result {
            Ok(f) => Box::new(f),
            Err(err) => Box::new(future::err(err)),
        }
    }

    fn start(&self, id: &str) -> Self::StartFuture {
        Box::new(
            self.client
                .container_api()
                .container_start(fensure_not_empty!(id), "")
                .map_err(Error::from)
                .map(|_| ()),
        )
    }

    fn stop(&self, id: &str) -> Self::StopFuture {
        Box::new(
            self.client
                .container_api()
                .container_stop(fensure_not_empty!(id), WAIT_BEFORE_KILL_SECONDS)
                .map_err(Error::from)
                .map(|_| ()),
        )
    }

    fn restart(&self, id: &str) -> Self::RestartFuture {
        Box::new(
            self.client
                .container_api()
                .container_restart(fensure_not_empty!(id), WAIT_BEFORE_KILL_SECONDS)
                .map_err(Error::from)
                .map(|_| ()),
        )
    }

    fn remove(&self, id: &str) -> Self::RemoveFuture {
        Box::new(
            self.client
                .container_api()
                .container_delete(
                    fensure_not_empty!(id),
                    /* remove volumes */ false,
                    /* force */ true,
                    /* remove link */ false,
                )
                .map_err(Error::from)
                .map(|_| ()),
        )
    }

    fn list(&self) -> Self::ListFuture {
        let mut filters = HashMap::new();
        filters.insert("label", LABELS.deref());

        let client_copy = self.client.clone();

        let result = serde_json::to_string(&filters)
            .and_then(|filters| {
                Ok(self.client
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
                            })
                            .flat_map(|(container, config)| {
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
                            })
                            .collect()
                    })
                    .map_err(Error::from))
            })
            .map_err(Error::from);

        match result {
            Ok(f) => Box::new(f),
            Err(err) => Box::new(future::err(err)),
        }
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        self
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

        let task = mri.stop("").then(|result| match result {
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

        let task = mri.stop("     ").then(|result| match result {
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
