// Copyright (c) Microsoft. All rights reserved.

use std::collections::{BTreeMap, HashMap};
use std::convert::TryInto;
use std::path::{Path, PathBuf};
use std::sync::Arc;
use std::time::{Duration, SystemTime, UNIX_EPOCH};
use std::{mem, process, str};

use failure::ResultExt;
use futures::{stream, StreamExt};
use log::{debug, info, Level};
use sysinfo::{DiskExt, ProcessExt, ProcessorExt, System, SystemExt};
use tokio::sync::Mutex;
use url::Url;

use docker::apis::{DockerApi, DockerApiClient};
use docker::models::{ContainerCreateBody, InlineResponse2001, Ipam, NetworkConfig};
use edgelet_core::{
    DiskInfo, LogOptions, MakeModuleRuntime, Module, ModuleRegistry, ModuleRuntime,
    ModuleRuntimeState, ProvisioningInfo, RegistryOperation, RuntimeOperation,
    SystemInfo as CoreSystemInfo, SystemResources,
};
use edgelet_settings::{
    ContentTrust, DockerConfig, Ipam as CoreIpam, MobyNetwork, ModuleSpec, RuntimeSettings,
    Settings,
};
use edgelet_utils::{ensure_not_empty_with_context, log_failure};

use crate::error::{Error, ErrorKind, Result};
use crate::module::{runtime_state, DockerModule, MODULE_TYPE as DOCKER_MODULE_TYPE};
use crate::notary;

type Deserializer = &'static mut serde_json::Deserializer<serde_json::de::IoRead<std::io::Empty>>;

const OWNER_LABEL_KEY: &str = "net.azure-devices.edge.owner";
const OWNER_LABEL_VALUE: &str = "Microsoft.Azure.Devices.Edge.Agent";
const ORIGINAL_IMAGE_LABEL_KEY: &str = "net.azure-devices.edge.original-image";
const LABELS: &[&str] = &["net.azure-devices.edge.owner=Microsoft.Azure.Devices.Edge.Agent"];

#[derive(Clone)]
pub struct DockerModuleRuntime {
    client: DockerApiClient,
    system_resources: Arc<Mutex<System>>,
    notary_registries: BTreeMap<String, PathBuf>,
    notary_lock: Arc<Mutex<BTreeMap<String, String>>>,
}

impl DockerModuleRuntime {
    fn merge_env(cur_env: Option<&[String]>, new_env: &BTreeMap<String, String>) -> Vec<String> {
        // build a new merged map containing string slices for keys and values
        // pointing into String instances in new_env
        let mut merged_env = BTreeMap::new();
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

    async fn get_notary_registries(settings: &Settings) -> Result<BTreeMap<String, PathBuf>> {
        if let Some(content_trust_map) = settings
            .moby_runtime()
            .content_trust()
            .and_then(ContentTrust::ca_certs)
        {
            debug!("Notary Content Trust is enabled");
            let home_dir: Arc<Path> = settings.homedir().into();
            let certd_url = settings.endpoints().aziot_certd_url().clone();
            let cert_client = aziot_cert_client_async::Client::new(
                aziot_cert_common_http::ApiVersion::V2020_09_01,
                http_common::Connector::new(&certd_url)
                    .map_err(|_| Error::from(ErrorKind::Docker))?, // TODO: Error Fix
            );

            let mut notary_registries = BTreeMap::new();
            for (registry_server_hostname, cert_id) in content_trust_map {
                let cert_buf = cert_client.get_cert(cert_id).await.map_err(|_| {
                    ErrorKind::NotaryRootCAReadError("Notary root CA read error".to_owned())
                })?;

                let config_path =
                    notary::notary_init(&home_dir, &registry_server_hostname, &cert_buf).map_err(
                        |_| ErrorKind::NotaryRootCAReadError("Notary init error".to_owned()),
                    )?;
                notary_registries.insert(registry_server_hostname.clone(), config_path);
            }

            Ok(notary_registries)
        } else {
            debug!("Notary Content Trust is disabled");
            Ok(BTreeMap::new())
        }
    }

    async fn check_for_notary_image(&self, config: &DockerConfig) -> Result<(String, bool)> {
        if let Some((notary_auth, gun, tag, config_path)) = self.get_notary_parameters(config) {
            let lock = self.notary_lock.clone();
            let mut notary_map = lock.lock().await;

            // Note `.entry()` cannot be used b/c the notary lookup is async
            let digest_from_notary = if let Some(digest) = notary_map.get(config.image()) {
                digest.clone()
            } else {
                let digest =
                    notary::notary_lookup(notary_auth.as_deref(), &gun, &tag, &config_path).await?;
                notary_map.insert(config.image().to_owned(), digest.clone());
                digest
            };

            let image_with_digest = format!("{}@{}", gun, digest_from_notary);
            if let Some(digest_from_manifest) = config.digest() {
                if digest_from_manifest == digest_from_notary {
                    info!("Digest from notary and Digest from manifest does match");
                    debug!(
                        "Digest from notary : {} and Digest from manifest : {} does match",
                        digest_from_notary, digest_from_manifest
                    );
                    Ok((image_with_digest, true))
                } else {
                    info!("Digest from notary and Digest from manifest does not match");
                    debug!(
                        "Digest from notary : {} and Digest from manifest : {} does not match",
                        digest_from_notary, digest_from_manifest
                    );
                    Err(Error::from(ErrorKind::NotaryDigestMismatch(
                        "notary digest mismatch with the manifest".to_owned(),
                    )))
                }
            } else {
                info!("No Digest from the manifest");
                Ok((image_with_digest, true))
            }
        } else {
            Ok((config.image().to_owned(), false))
        }
    }

    fn get_notary_parameters(
        &self,
        config: &DockerConfig,
    ) -> Option<(Option<String>, Arc<str>, String, PathBuf)> {
        // check if the serveraddress exists & check if it exists in notary_registries
        let registry_auth = config.auth();
        let (registry_hostname, registry_username, registry_password) = match registry_auth {
            Some(a) => (a.serveraddress(), a.username(), a.password()),
            None => (None, None, None),
        };
        let hostname = registry_hostname?;
        let config_path = self.notary_registries.get(hostname)?;
        info!("{} is enabled for notary content trust", hostname);
        let notary_auth = match (registry_username, registry_password) {
            (None, None) => None,
            (username, password) => {
                let notary_auth = format!(
                    "{}:{}",
                    username.unwrap_or_default(),
                    password.unwrap_or_default()
                );
                let notary_auth = base64::encode(&notary_auth);
                Some(notary_auth)
            }
        };
        let mut image_with_tag_parts = config.image().split(':');
        let gun = image_with_tag_parts
            .next()
            .expect("split always returns atleast one element")
            .to_owned();
        let gun: Arc<str> = gun.into();
        let tag = image_with_tag_parts.next().unwrap_or("latest").to_owned();

        Some((notary_auth, gun, tag, config_path.to_path_buf()))
    }
}

impl std::fmt::Debug for DockerModuleRuntime {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("DockerModuleRuntime").finish()
    }
}

#[async_trait::async_trait]
impl ModuleRegistry for DockerModuleRuntime {
    type Error = Error;
    type Config = DockerConfig;

    async fn pull(&self, config: &Self::Config) -> Result<()> {
        let (image, is_content_trust_enabled) = self.check_for_notary_image(config).await?;
        if is_content_trust_enabled {
            info!("Pulling image via digest {}...", image);
        } else {
            info!("Pulling image via tag {}...", image);
        }

        let creds = match config.auth() {
            Some(a) => {
                let json = serde_json::to_string(&a).with_context(|_| {
                    ErrorKind::RegistryOperation(RegistryOperation::PullImage(image.clone()))
                })?;
                base64::encode_config(&json, base64::URL_SAFE)
            }
            None => String::new(),
        };

        self.client
            .image_create(&image, "", "", "", "", &creds, "")
            .await
            .map_err(|err| {
                Error::from_docker_error(
                    err,
                    ErrorKind::RegistryOperation(RegistryOperation::PullImage(image.clone())),
                )
            })?;

        info!("Successfully pulled image {}", image);
        Ok(())
    }

    async fn remove(&self, name: &str) -> Result<()> {
        info!("Removing image {}...", name);

        if let Err(err) = ensure_not_empty_with_context(name, || {
            ErrorKind::RegistryOperation(RegistryOperation::RemoveImage(name.to_string()))
        }) {
            return Err(Error::from(err));
        }

        self.client
            .image_delete(name, false, false)
            .await
            .map_err(|e| {
                let err = Error::from_docker_error(
                    e,
                    ErrorKind::RegistryOperation(RegistryOperation::RemoveImage(name.to_string())),
                );
                log_failure(Level::Warn, &err);
                err
            })?;

        info!("Successfully removed image {}", name);
        Ok(())
    }
}

#[async_trait::async_trait]
impl MakeModuleRuntime for DockerModuleRuntime {
    type Config = DockerConfig;
    type Settings = Settings;
    type ModuleRuntime = Self;
    type Error = Error;

    async fn make_runtime(settings: &Settings) -> Result<Self::ModuleRuntime> {
        info!("Initializing module runtime...");

        let client = init_client(settings.moby_runtime().uri()).await?;
        let notary_registries = Self::get_notary_registries(settings).await?;
        create_network_if_missing(settings, &client).await?;

        // to avoid excessive FD usage, we will not allow sysinfo to keep files open.
        sysinfo::set_open_files_limit(0);
        let system_resources = System::new_all();
        info!("Successfully initialized module runtime");

        let runtime = Self {
            client,
            system_resources: Arc::new(Mutex::new(system_resources)),
            notary_registries,
            notary_lock: Arc::new(Mutex::new(BTreeMap::new())),
        };

        Ok(runtime)
    }
}

async fn init_client(docker_url: &Url) -> Result<DockerApiClient> {
    // // build the hyper client
    // let client =
    //     Client::builder().build(UrlConnector::new(docker_url).context(ErrorKind::Initialization)?);

    // // extract base path - the bit that comes after the scheme
    // let base_path = docker_url
    //     .to_base_path()
    //     .context(ErrorKind::Initialization)?;
    // let mut configuration = Configuration::new(client);
    // configuration.base_path = base_path
    //     .to_str()
    //     .ok_or(ErrorKind::Initialization)?
    //     .to_string();

    // let scheme = docker_url.scheme().to_string();
    // configuration.uri_composer = Box::new(move |base_path, path| {
    //     Ok(UrlConnector::build_hyper_uri(&scheme, base_path, path)
    //         .context(ErrorKind::Initialization)?)
    // });

    // Ok(DockerClient::new(APIClient::new(configuration)))

    unimplemented!();
}

async fn create_network_if_missing(settings: &Settings, client: &DockerApiClient) -> Result<()> {
    let (enable_i_pv6, ipam) = get_ipv6_settings(settings.moby_runtime().network());
    let network_id = settings.moby_runtime().network().name();
    info!("Using runtime network id {}", network_id);

    let filter = format!(r#"{{"name":{{"{}":true}}}}"#, network_id);
    let existing_iotedge_networks = client.network_list(&filter).await.map_err(|err| {
        let e = Error::from_docker_error(err, ErrorKind::RuntimeOperation(RuntimeOperation::Init));
        log_failure(Level::Warn, &e);
        e
    })?;

    if existing_iotedge_networks.is_empty() {
        let mut network_config =
            NetworkConfig::new(network_id.to_string()).with_enable_i_pv6(enable_i_pv6);

        if let Some(ipam_config) = ipam {
            network_config.set_IPAM(ipam_config);
        };

        client.network_create(network_config).await.map_err(|err| {
            let e =
                Error::from_docker_error(err, ErrorKind::RuntimeOperation(RuntimeOperation::Init));
            log_failure(Level::Warn, &e);
            e
        })?;
    }

    Ok(())
}

fn get_ipv6_settings(network_configuration: &MobyNetwork) -> (bool, Option<Ipam>) {
    if let MobyNetwork::Network(network) = network_configuration {
        let ipv6 = network.ipv6().unwrap_or_default();
        network.ipam().and_then(CoreIpam::config).map_or_else(
            || (ipv6, None),
            |ipam_config| {
                let config = ipam_config
                    .iter()
                    .map(|ipam_config| {
                        let mut config_map = HashMap::new();
                        if let Some(gateway_config) = ipam_config.gateway() {
                            config_map.insert("Gateway".to_string(), gateway_config.to_string());
                        };

                        if let Some(subnet_config) = ipam_config.subnet() {
                            config_map.insert("Subnet".to_string(), subnet_config.to_string());
                        };

                        if let Some(ip_range_config) = ipam_config.ip_range() {
                            config_map.insert("IPRange".to_string(), ip_range_config.to_string());
                        };

                        config_map
                    })
                    .collect();

                (ipv6, Some(Ipam::new().with_config(config)))
            },
        )
    } else {
        (false, None)
    }
}

#[async_trait::async_trait]
impl ModuleRuntime for DockerModuleRuntime {
    type Error = Error;
    type Config = DockerConfig;
    type Module = DockerModule;
    type ModuleRegistry = Self;

    async fn create(&self, module: ModuleSpec<Self::Config>) -> Result<()> {
        info!("Creating module {}...", module.name());

        // we only want "docker" modules
        if module.r#type() != DOCKER_MODULE_TYPE {
            return Err(Error::from(ErrorKind::InvalidModuleType(
                module.r#type().to_string(),
            )));
        }

        let (image, is_content_trust_enabled) =
            self.check_for_notary_image(module.config()).await?;
        if is_content_trust_enabled {
            info!("Creating image via digest {}...", image);
        } else {
            info!("Creating image via tag {}...", image);
        }

        debug!("Creating container {} with image {}", module.name(), image);
        let create_options = module.config().create_options().clone();
        let merged_env = DockerModuleRuntime::merge_env(create_options.env(), module.env());

        let mut labels = create_options
            .labels()
            .cloned()
            .unwrap_or_else(BTreeMap::new);
        labels.insert(OWNER_LABEL_KEY.to_string(), OWNER_LABEL_VALUE.to_string());
        labels.insert(
            ORIGINAL_IMAGE_LABEL_KEY.to_string(),
            module.config().image().to_string(),
        );

        debug!("Creating container {} with image {}", module.name(), image);

        let create_options = create_options
            .with_image(image)
            .with_env(merged_env)
            .with_labels(labels);

        // Here we don't add the container to the iot edge docker network as the edge-agent is expected to do that.
        // It contains the logic to add a container to the iot edge network only if a network is not already specified.
        self.client
            .container_create(create_options, module.name())
            .await
            .map_err(|e| {
                Error::from_docker_error(
                    e,
                    ErrorKind::RuntimeOperation(RuntimeOperation::CreateModule(
                        module.name().to_string(),
                    )),
                )
            })?;

        Ok(())
    }

    async fn get(&self, id: &str) -> Result<(Self::Module, ModuleRuntimeState)> {
        debug!("Getting module {}...", id);

        ensure_not_empty_with_context(id, || {
            ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(id.to_owned()))
        })
        .map_err(Error::from)?;

        let response = self
            .client
            .container_inspect(id, false)
            .await
            .map_err(|_| {
                Error::from(ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(
                    id.to_owned(),
                )))
            })?;

        let name: String = response
            .name()
            .ok_or_else(|| {
                ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(id.to_string()))
            })?
            .to_owned();
        let config = DockerConfig::new(name.clone(), ContainerCreateBody::new(), None, None)
            .map_err(|_| {
                ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(id.to_string()))
            })?;
        let module = DockerModule::new(self.client.clone(), name, config).with_context(|_| {
            ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(id.to_string()))
        })?;
        let state = runtime_state(response.id(), response.state());

        Ok((module, state))
    }

    async fn start(&self, id: &str) -> Result<()> {
        info!("Starting module {}...", id);

        ensure_not_empty_with_context(id, || {
            ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(id.to_owned()))
        })
        .map_err(Error::from)?;

        self.client.container_start(id, "").await.map_err(|e| {
            let err = Error::from_docker_error(
                e,
                ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(id.to_owned())),
            );
            log_failure(Level::Warn, &err);
            err
        })
    }

    async fn stop(&self, id: &str, wait_before_kill: Option<Duration>) -> Result<()> {
        info!("Stopping module {}...", id);

        ensure_not_empty_with_context(id, || {
            ErrorKind::RuntimeOperation(RuntimeOperation::StopModule(id.to_owned()))
        })
        .map_err(Error::from)?;

        #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
        let wait_timeout = wait_before_kill.map(|s| match s.as_secs() {
            s if s > i32::max_value() as u64 => i32::max_value(),
            s => s as i32,
        });

        self.client
            .container_stop(id, wait_timeout)
            .await
            .map_err(|e| {
                let err = Error::from_docker_error(
                    e,
                    ErrorKind::RuntimeOperation(RuntimeOperation::StopModule(id.to_owned())),
                );
                log_failure(Level::Warn, &err);
                err
            })
    }

    async fn restart(&self, id: &str) -> Result<()> {
        info!("Restarting module {}...", id);
        ensure_not_empty_with_context(id, || {
            ErrorKind::RuntimeOperation(RuntimeOperation::RestartModule(id.to_owned()))
        })
        .map_err(Error::from)?;

        self.client.container_restart(id, None).await.map_err(|e| {
            let err = Error::from_docker_error(
                e,
                ErrorKind::RuntimeOperation(RuntimeOperation::RestartModule(id.to_owned())),
            );
            log_failure(Level::Warn, &err);
            err
        })
    }

    async fn remove(&self, id: &str) -> Result<()> {
        info!("Removing module {}...", id);

        ensure_not_empty_with_context(id, || {
            ErrorKind::RuntimeOperation(RuntimeOperation::RemoveModule(id.to_owned()))
        })
        .map_err(Error::from)?;

        self.client
            .container_delete(
                &id, /* remove volumes */ false, /* force */ true,
                /* remove link */ false,
            )
            .await
            .map_err(|e| {
                let err = Error::from_docker_error(
                    e,
                    ErrorKind::RuntimeOperation(RuntimeOperation::RemoveModule(id.to_owned())),
                );
                log_failure(Level::Warn, &err);
                err
            })
    }

    async fn system_info(&self) -> Result<CoreSystemInfo> {
        info!("Querying system info...");

        // Provisioning information is no longer available in aziot-edged. This information should
        // be emitted from Identity Service
        let provisioning = ProvisioningInfo {
            r#type: "ProvisioningType".into(),
            dynamic_reprovisioning: false,
            always_reprovision_on_startup: false,
        };

        let system_info = self.client.system_info().await.map_err(|e| {
            Error::from_docker_error(e, ErrorKind::RuntimeOperation(RuntimeOperation::SystemInfo))
        })?;

        let system_info = CoreSystemInfo {
            os_type: system_info
                .os_type()
                .unwrap_or(&String::from("Unknown"))
                .to_string(),
            architecture: system_info
                .architecture()
                .unwrap_or(&String::from("Unknown"))
                .to_string(),
            version: edgelet_core::version_with_source_version(),
            provisioning,
            cpus: system_info.NCPU().unwrap_or_default(),
            virtualized: match edgelet_core::is_virtualized_env() {
                Ok(Some(true)) => "yes",
                Ok(Some(false)) => "no",
                Ok(None) | Err(_) => "unknown",
            }
            .to_string(),
            kernel_version: system_info
                .kernel_version()
                .map(std::string::ToString::to_string)
                .unwrap_or_default(),
            operating_system: system_info
                .operating_system()
                .map(std::string::ToString::to_string)
                .unwrap_or_default(),
            server_version: system_info
                .server_version()
                .map(std::string::ToString::to_string)
                .unwrap_or_default(),
        };
        info!("Successfully queried system info");
        Ok(system_info)
    }

    async fn system_resources(&self) -> Result<SystemResources> {
        info!("Querying system resources...");

        let uptime: u64 = {
            let mut info: libc::sysinfo = unsafe { mem::zeroed() };
            let ret = unsafe { libc::sysinfo(&mut info) };
            if ret == 0 {
                info.uptime.try_into().unwrap_or_default()
            } else {
                0
            }
        };

        // Get system resources
        let mut system_resources = self.system_resources.as_ref().lock().await;
        system_resources.refresh_system();
        let start_time = process::id()
            .try_into()
            .map(|id| {
                system_resources.refresh_process(id);
                system_resources
                    .get_process(id)
                    .map(|p| p.start_time())
                    .unwrap_or_default()
            })
            .unwrap_or_default();

        let current_time = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap_or_default()
            .as_secs();

        let used_cpu = system_resources.get_global_processor_info().get_cpu_usage();
        let total_memory = system_resources.get_total_memory() * 1000;
        let used_memory = system_resources.get_used_memory() * 1000;

        system_resources.refresh_disks();
        let disks = system_resources
            .get_disks()
            .iter()
            .map(|disk| {
                DiskInfo::new(
                    disk.get_name().to_string_lossy().into_owned(),
                    disk.get_available_space(),
                    disk.get_total_space(),
                    String::from_utf8_lossy(disk.get_file_system()).into_owned(),
                    format!("{:?}", disk.get_type()),
                )
            })
            .collect();

        // Get Docker Stats
        // Note a for_each loop is used for simplicity with async operations
        // While a stream could be used for parallel operations, it isn't necessary here
        let modules = self.list().await?;
        let mut docker_stats: Vec<serde_json::Value> = Vec::with_capacity(modules.len());
        for module in modules {
            let stats = self
                .client
                .container_stats(module.name(), false)
                .await
                .map_err(|e| {
                    Error::from_docker_error(
                        e,
                        ErrorKind::RuntimeOperation(RuntimeOperation::SystemResources),
                    )
                })?;

            docker_stats.push(stats);
        }
        let docker_stats = serde_json::to_string(&docker_stats).map_err(|_| {
            Error::from(ErrorKind::RuntimeOperation(
                RuntimeOperation::SystemResources,
            ))
        })?;

        Ok(SystemResources::new(
            uptime,
            current_time - start_time,
            used_cpu.into(),
            used_memory,
            total_memory,
            disks,
            docker_stats,
        ))
    }

    async fn list(&self) -> Result<Vec<Self::Module>> {
        debug!("Listing modules...");

        let mut filters = HashMap::new();
        filters.insert("label", LABELS);
        let filters = serde_json::to_string(&filters)
            .context(ErrorKind::RuntimeOperation(RuntimeOperation::ListModules))
            .map_err(Error::from)?;

        let containers = self
            .client
            .container_list(
                true,  /*all*/
                0,     /*limit*/
                false, /*size*/
                &filters,
            )
            .await
            .map_err(|e| {
                Error::from_docker_error(
                    e,
                    ErrorKind::RuntimeOperation(RuntimeOperation::ListModules),
                )
            })?;

        let result = containers
            .iter()
            .flat_map(|container| {
                DockerConfig::new(
                    container.image().to_string(),
                    ContainerCreateBody::new().with_labels(
                        container
                            .labels()
                            .iter()
                            .map(|(k, v)| (k.to_string(), v.to_string()))
                            .collect(),
                    ),
                    None,
                    None,
                )
                .map(|config| {
                    (
                        container,
                        config.with_image_hash(container.image_id().clone()),
                    )
                })
            })
            .flat_map(|(container, config)| {
                DockerModule::new(
                    self.client.clone(),
                    container
                        .names()
                        .iter()
                        .next()
                        .map_or("Unknown", |s| &s[1..])
                        .to_string(),
                    config,
                )
            })
            .collect();

        Ok(result)
    }

    async fn list_with_details(&self) -> Result<Vec<(Self::Module, ModuleRuntimeState)>> {
        let mut result = Vec::new();
        for module in self.list().await? {
            // Note, if error calling just drop module from list
            if let Ok(module_with_details) = self.get(module.name()).await {
                result.push(module_with_details);
            }
        }

        Ok(result)
    }

    async fn logs(&self, id: &str, options: &LogOptions) -> Result<hyper::Body> {
        info!("Getting logs for module {}...", id);

        self.client
            .container_logs(
                id,
                options.follow(),
                true,
                true,
                options.since(),
                options.until(),
                options.timestamps(),
                &options.tail().to_string(),
            )
            .await
            .map_err(|e| {
                let err = Error::from_docker_error(
                    e,
                    ErrorKind::RuntimeOperation(RuntimeOperation::GetModuleLogs(id.to_owned())),
                );
                log_failure(Level::Warn, &err);
                err
            })
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        self
    }

    async fn remove_all(&self) -> Result<()> {
        stream::iter(self.list().await?)
            .then(|module| async move { ModuleRuntime::remove(self, module.name()).await })
            .collect::<Vec<Result<_>>>()
            .await
            .into_iter()
            .collect::<Result<Vec<_>>>()?;

        Ok(())
    }

    async fn stop_all(&self, wait_before_kill: Option<Duration>) -> Result<()> {
        stream::iter(self.list().await?)
            .then(|module| async move { self.stop(module.name(), wait_before_kill).await })
            .collect::<Vec<Result<_>>>()
            .await
            .into_iter()
            .collect::<Result<Vec<_>>>()?;

        Ok(())
    }

    async fn module_top(&self, id: &str) -> Result<Vec<i32>> {
        let top_response = self.client.container_top(id, "").await.map_err(|e| {
            let err = Error::from_docker_error(
                e,
                ErrorKind::RuntimeOperation(RuntimeOperation::TopModule(id.to_owned())),
            );
            log_failure(Level::Warn, &err);
            err
        })?;

        let pids = parse_top_response::<Deserializer>(&top_response).with_context(|_| {
            ErrorKind::RuntimeOperation(RuntimeOperation::TopModule(id.to_owned()))
        })?;

        Ok(pids)
    }
}

fn parse_top_response<'de, D>(resp: &InlineResponse2001) -> std::result::Result<Vec<i32>, D::Error>
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
            Ok(pid)
        })
        .collect();
    Ok(pids?)
}

// #[cfg(test)]
// mod tests {
//     use super::{
//         authenticate, future, list_with_details, parse_get_response, AuthId, Authenticator,
//         BTreeMap, Body, CoreSystemInfo, Deserializer, DockerModuleRuntime, DockerModuleTop,
//         Duration, Error, ErrorKind, Future, InlineResponse200, LogOptions, MakeModuleRuntime,
//         Module, ModuleId, ModuleRuntime, ModuleRuntimeState, ModuleSpec, Pid, Request, Stream,
//         SystemResources,
//     };

//     use std::path::Path;

//     use futures::stream::Empty;

//     use edgelet_core::{
//         settings::AutoReprovisioningMode, Connect, Endpoints, Listen, ModuleRegistry, ModuleTop,
//         RuntimeSettings, WatchdogSettings,
//     };

//     #[test]
//     fn merge_env_empty() {
//         let cur_env = Some(&[][..]);
//         let new_env = BTreeMap::new();
//         assert_eq!(0, DockerModuleRuntime::merge_env(cur_env, &new_env).len());
//     }

//     #[test]
//     fn merge_env_new_empty() {
//         let cur_env = Some(vec!["k1=v1".to_string(), "k2=v2".to_string()]);
//         let new_env = BTreeMap::new();
//         let mut merged_env =
//             DockerModuleRuntime::merge_env(cur_env.as_ref().map(AsRef::as_ref), &new_env);
//         merged_env.sort();
//         assert_eq!(vec!["k1=v1", "k2=v2"], merged_env);
//     }

//     #[test]
//     fn merge_env_extend_new() {
//         let cur_env = Some(vec!["k1=v1".to_string(), "k2=v2".to_string()]);
//         let mut new_env = BTreeMap::new();
//         new_env.insert("k3".to_string(), "v3".to_string());
//         let mut merged_env =
//             DockerModuleRuntime::merge_env(cur_env.as_ref().map(AsRef::as_ref), &new_env);
//         merged_env.sort();
//         assert_eq!(vec!["k1=v1", "k2=v2", "k3=v3"], merged_env);
//     }

//     #[test]
//     fn merge_env_extend_replace_new() {
//         let cur_env = Some(vec!["k1=v1".to_string(), "k2=v2".to_string()]);
//         let mut new_env = BTreeMap::new();
//         new_env.insert("k2".to_string(), "v02".to_string());
//         new_env.insert("k3".to_string(), "v3".to_string());
//         let mut merged_env =
//             DockerModuleRuntime::merge_env(cur_env.as_ref().map(AsRef::as_ref), &new_env);
//         merged_env.sort();
//         assert_eq!(vec!["k1=v1", "k2=v2", "k3=v3"], merged_env);
//     }

//     #[test]
//     fn list_with_details_filters_out_deleted_containers() {
//         let runtime = prepare_module_runtime_with_known_modules();

//         assert_eq!(
//             runtime.list_with_details().collect().wait().unwrap(),
//             vec![
//                 (
//                     runtime.modules[0].clone(),
//                     ModuleRuntimeState::default().with_pid(Some(1000))
//                 ),
//                 (
//                     runtime.modules[3].clone(),
//                     ModuleRuntimeState::default().with_pid(Some(4000))
//                 ),
//             ]
//         );
//     }

//     #[test]
//     fn authenticate_returns_none_when_no_pid_provided() {
//         let runtime = prepare_module_runtime_with_known_modules();
//         let req = Request::default();

//         let auth_id = runtime.authenticate(&req).wait().unwrap();

//         assert_eq!(AuthId::None, auth_id);
//     }

//     #[test]
//     fn authenticate_returns_none_when_unknown_pid_provided() {
//         let runtime = prepare_module_runtime_with_known_modules();
//         let mut req = Request::default();
//         req.extensions_mut().insert(Pid::Value(1));

//         let auth_id = runtime.authenticate(&req).wait().unwrap();

//         assert_eq!(AuthId::None, auth_id);
//     }

//     #[test]
//     fn authenticate_returns_none_when_expected_module_not_exist_anymore_with_top() {
//         let runtime = prepare_module_runtime_with_known_modules();
//         let mut req = Request::default();
//         req.extensions_mut().insert(Pid::Value(2000));
//         req.extensions_mut().insert(ModuleId::from("b"));

//         let auth_id = runtime.authenticate(&req).wait().unwrap();

//         assert_eq!(AuthId::None, auth_id);
//     }

//     #[test]
//     fn authenticate_returns_none_when_expected_module_not_found() {
//         let runtime = prepare_module_runtime_with_known_modules();
//         let mut req = Request::default();
//         req.extensions_mut().insert(Pid::Value(1000));
//         req.extensions_mut().insert(ModuleId::from("x"));

//         let auth_id = runtime.authenticate(&req).wait().unwrap();

//         assert_eq!(AuthId::None, auth_id);
//     }

//     #[test]
//     fn authenticate_returns_any_when_any_provided() {
//         let runtime = prepare_module_runtime_with_known_modules();
//         let mut req = Request::default();
//         req.extensions_mut().insert(Pid::Any);

//         let auth_id = runtime.authenticate(&req).wait().unwrap();

//         assert_eq!(AuthId::Any, auth_id);
//     }

//     #[test]
//     fn authenticate_returns_any_when_module_pid_provided() {
//         let runtime = prepare_module_runtime_with_known_modules();
//         let mut req = Request::default();
//         req.extensions_mut().insert(Pid::Value(1000));
//         req.extensions_mut().insert(ModuleId::from("a"));

//         let auth_id = runtime.authenticate(&req).wait().unwrap();

//         assert_eq!(AuthId::Value("a".into()), auth_id);
//     }

//     #[test]
//     fn authenticate_returns_any_when_any_pid_of_module_provided() {
//         let runtime = prepare_module_runtime_with_known_modules();
//         let mut req = Request::default();
//         req.extensions_mut().insert(Pid::Value(4001));
//         req.extensions_mut().insert(ModuleId::from("d"));

//         let auth_id = runtime.authenticate(&req).wait().unwrap();

//         assert_eq!(AuthId::Value("d".into()), auth_id);
//     }

//     fn prepare_module_runtime_with_known_modules() -> TestModuleList {
//         TestModuleList {
//             modules: vec![
//                 TestModule {
//                     name: "a".to_string(),
//                     runtime_state_behavior: TestModuleRuntimeStateBehavior::Default,
//                     process_ids: vec![1000],
//                 },
//                 TestModule {
//                     name: "b".to_string(),
//                     runtime_state_behavior: TestModuleRuntimeStateBehavior::NotFound,
//                     process_ids: vec![2000, 2001],
//                 },
//                 TestModule {
//                     name: "c".to_string(),
//                     runtime_state_behavior: TestModuleRuntimeStateBehavior::NotFound,
//                     process_ids: vec![3000],
//                 },
//                 TestModule {
//                     name: "d".to_string(),
//                     runtime_state_behavior: TestModuleRuntimeStateBehavior::Default,
//                     process_ids: vec![4000, 4001],
//                 },
//             ],
//         }
//     }

//     #[test]
//     fn parse_get_response_returns_the_name() {
//         let response = InlineResponse200::new().with_name("hello".to_string());
//         let name = parse_get_response::<Deserializer>(&response);
//         assert!(name.is_ok());
//         assert_eq!("hello".to_string(), name.unwrap());
//     }

//     #[test]
//     fn parse_get_response_returns_error_when_name_is_missing() {
//         let response = InlineResponse200::new();
//         let name = parse_get_response::<Deserializer>(&response);
//         assert!(name.is_err());
//         assert_eq!("missing field `Name`", format!("{}", name.unwrap_err()));
//     }

//     #[derive(Clone)]
//     struct TestConfig;

//     struct TestSettings;

//     impl RuntimeSettings for TestSettings {
//         type Config = TestConfig;

//         fn agent(&self) -> &ModuleSpec {
//             unimplemented!()
//         }

//         fn agent_mut(&mut self) -> &mut ModuleSpec {
//             unimplemented!()
//         }

//         fn hostname(&self) -> &str {
//             unimplemented!()
//         }

//         fn connect(&self) -> &Connect {
//             unimplemented!()
//         }

//         fn listen(&self) -> &Listen {
//             unimplemented!()
//         }

//         fn homedir(&self) -> &Path {
//             unimplemented!()
//         }

//         fn watchdog(&self) -> &WatchdogSettings {
//             unimplemented!()
//         }

//         fn endpoints(&self) -> &Endpoints {
//             unimplemented!()
//         }

//         fn edge_ca_cert(&self) -> Option<&str> {
//             unimplemented!()
//         }

//         fn edge_ca_key(&self) -> Option<&str> {
//             unimplemented!()
//         }

//         fn trust_bundle_cert(&self) -> Option<&str> {
//             unimplemented!()
//         }

//         fn manifest_trust_bundle_cert(&self) -> Option<&str> {
//             unimplemented!()
//         }

//         fn auto_reprovisioning_mode(&self) -> &AutoReprovisioningMode {
//             unimplemented!()
//         }
//     }

//     #[derive(Clone, Copy, Debug, PartialEq)]
//     enum TestModuleRuntimeStateBehavior {
//         Default,
//         NotFound,
//     }

//     #[derive(Clone, Debug, PartialEq)]
//     struct TestModule {
//         name: String,
//         runtime_state_behavior: TestModuleRuntimeStateBehavior,
//         process_ids: Vec<i32>,
//     }

//     impl Module for TestModule {
//         type Config = TestConfig;
//         type Error = Error;
//         type RuntimeStateFuture = FutureResult<ModuleRuntimeState, Self::Error>;

//         fn name(&self) -> &str {
//             &self.name
//         }

//         fn type_(&self) -> &str {
//             ""
//         }

//         fn config(&self) -> &Self::Config {
//             &TestConfig
//         }

//         fn runtime_state(&self) -> Self::RuntimeStateFuture {
//             match self.runtime_state_behavior {
//                 TestModuleRuntimeStateBehavior::Default => {
//                     let top_pid = self.process_ids.first().cloned();
//                     future::ok(ModuleRuntimeState::default().with_pid(top_pid))
//                 }
//                 TestModuleRuntimeStateBehavior::NotFound => {
//                     future::err(ErrorKind::NotFound(String::new()).into())
//                 }
//             }
//         }
//     }

//     #[derive(Clone)]
//     struct TestModuleList {
//         modules: Vec<TestModule>,
//     }

//     impl ModuleRegistry for TestModuleList {
//         type Error = Error;
//         type PullFuture = FutureResult<(), Self::Error>;
//         type RemoveFuture = FutureResult<(), Self::Error>;
//         type Config = TestConfig;

//         fn pull(&self, _config: &Self::Config) -> Self::PullFuture {
//             unimplemented!()
//         }

//         fn remove(&self, _name: &str) -> Self::RemoveFuture {
//             unimplemented!()
//         }
//     }

//     impl DockerModuleTop for TestModule {
//         type Error = Error;
//         type ModuleTopFuture = FutureResult<ModuleTop, Self::Error>;

//         fn top(&self) -> Self::ModuleTopFuture {
//             match self.runtime_state_behavior {
//                 TestModuleRuntimeStateBehavior::Default => {
//                     future::ok(ModuleTop::new(self.name.clone(), self.process_ids.clone()))
//                 }
//                 TestModuleRuntimeStateBehavior::NotFound => {
//                     future::err(ErrorKind::NotFound(String::new()).into())
//                 }
//             }
//         }
//     }

//     impl MakeModuleRuntime for TestModuleList {
//         type Config = TestConfig;
//         type ModuleRuntime = Self;
//         type Settings = TestSettings;
//         type Error = Error;
//         type Future = FutureResult<Self, Self::Error>;

//         fn make_runtime(_settings: Self::Settings) -> Self::Future {
//             unimplemented!()
//         }
//     }

//     impl ModuleRuntime for TestModuleList {
//         type Error = Error;
//         type Config = TestConfig;
//         type Module = TestModule;
//         type ModuleRegistry = Self;
//         type Chunk = String;
//         type Logs = Empty<Self::Chunk, Self::Error>;

//         type CreateFuture = FutureResult<(), Self::Error>;
//         type GetFuture = FutureResult<(Self::Module, ModuleRuntimeState), Self::Error>;
//         type ListFuture = FutureResult<Vec<Self::Module>, Self::Error>;
//         type ListWithDetailsStream =
//             Box<dyn Stream<Item = (Self::Module, ModuleRuntimeState), Error = Self::Error> + Send>;
//         type LogsFuture = FutureResult<Self::Logs, Self::Error>;
//         type RemoveFuture = FutureResult<(), Self::Error>;
//         type RestartFuture = FutureResult<(), Self::Error>;
//         type StartFuture = FutureResult<(), Self::Error>;
//         type StopFuture = FutureResult<(), Self::Error>;
//         type SystemInfoFuture = FutureResult<CoreSystemInfo, Self::Error>;
//         type SystemResourcesFuture =
//             Box<dyn Future<Item = SystemResources, Error = Self::Error> + Send>;
//         type RemoveAllFuture = FutureResult<(), Self::Error>;
//         type StopAllFuture = FutureResult<(), Self::Error>;

//         fn create(&self, _module: ModuleSpec) -> Self::CreateFuture {
//             unimplemented!()
//         }

//         fn get(&self, _id: &str) -> Self::GetFuture {
//             unimplemented!()
//         }

//         fn start(&self, _id: &str) -> Self::StartFuture {
//             unimplemented!()
//         }

//         fn stop(&self, _id: &str, _wait_before_kill: Option<Duration>) -> Self::StopFuture {
//             unimplemented!()
//         }

//         fn restart(&self, _id: &str) -> Self::RestartFuture {
//             unimplemented!()
//         }

//         fn remove(&self, _id: &str) -> Self::RemoveFuture {
//             unimplemented!()
//         }

//         fn system_info(&self) -> Self::SystemInfoFuture {
//             unimplemented!()
//         }

//         fn system_resources(&self) -> Self::SystemResourcesFuture {
//             unimplemented!()
//         }

//         fn list(&self) -> Self::ListFuture {
//             future::ok(self.modules.clone())
//         }

//         fn list_with_details(&self) -> Self::ListWithDetailsStream {
//             list_with_details(self)
//         }

//         fn logs(&self, _id: &str, _options: &LogOptions) -> Self::LogsFuture {
//             unimplemented!()
//         }

//         fn registry(&self) -> &Self::ModuleRegistry {
//             self
//         }

//         fn remove_all(&self) -> Self::RemoveAllFuture {
//             unimplemented!()
//         }

//         fn stop_all(&self, _wait_before_kill: Option<Duration>) -> Self::StopAllFuture {
//             unimplemented!()
//         }
//     }

//     impl Authenticator for TestModuleList {
//         type Error = Error;
//         type Request = Request<Body>;
//         type AuthenticateFuture = Box<dyn Future<Item = AuthId, Error = Self::Error> + Send>;

//         fn authenticate(&self, req: &Self::Request) -> Self::AuthenticateFuture {
//             authenticate(self, req)
//         }
//     }
// }
