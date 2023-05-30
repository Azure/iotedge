// Copyright (c) Microsoft. All rights reserved.

use std::collections::{BTreeMap, HashMap};
use std::sync::Arc;
use std::time::{Duration, SystemTime, UNIX_EPOCH};
use std::{process, str};

use anyhow::Context;
use sysinfo::{CpuExt, DiskExt, PidExt, ProcessExt, System, SystemExt};
use tokio::sync::mpsc::UnboundedSender;
use tokio::sync::Mutex;
use url::Url;

use docker::apis::{Configuration, DockerApi, DockerApiClient};
use docker::models::{ContainerCreateBody, HostConfig, InlineResponse2001, Ipam, NetworkConfig};
use edgelet_core::{
    DiskInfo, LogOptions, Module, ModuleAction, ModuleRegistry, ModuleRuntime, ModuleRuntimeState,
    RegistryOperation, RuntimeOperation, SystemInfo as CoreSystemInfo, SystemResources, UrlExt,
};
use edgelet_settings::{
    DockerConfig, Ipam as CoreIpam, MobyNetwork, ModuleSpec, RuntimeSettings, Settings,
};
use edgelet_utils::ensure_not_empty;
use http_common::Connector;

use crate::error::Error;
use crate::module::{runtime_state, DockerModule, MODULE_TYPE as DOCKER_MODULE_TYPE};
use crate::{ImagePruneData, MakeModuleRuntime};

type Deserializer = &'static mut serde_json::Deserializer<serde_json::de::IoRead<std::io::Empty>>;

const OWNER_LABEL_KEY: &str = "net.azure-devices.edge.owner";
const OWNER_LABEL_VALUE: &str = "Microsoft.Azure.Devices.Edge.Agent";
const ORIGINAL_IMAGE_LABEL_KEY: &str = "net.azure-devices.edge.original-image";
const LABELS: &[&str] = &["net.azure-devices.edge.owner=Microsoft.Azure.Devices.Edge.Agent"];

#[derive(Clone)]
pub struct DockerModuleRuntime<C> {
    client: DockerApiClient<C>,
    system_resources: Arc<Mutex<System>>,
    create_socket_channel: UnboundedSender<ModuleAction>,
    allow_elevated_docker_permissions: bool,
    additional_info: BTreeMap<String, String>,
    image_use_data: ImagePruneData,
}

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
            tokens
                .next()
                .map(|key| (key, tokens.next().unwrap_or_default()))
        }));
    }

    // finally build a new Vec<String>; we alloc new strings here
    merged_env
        .iter()
        .map(|(key, value)| format!("{}={}", key, value))
        .collect()
}

impl<C> std::fmt::Debug for DockerModuleRuntime<C> {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("DockerModuleRuntime").finish()
    }
}

#[async_trait::async_trait]
impl<C> ModuleRegistry for DockerModuleRuntime<C>
where
    C: Clone + hyper::client::connect::Connect + Send + Sync + 'static,
{
    type Config = DockerConfig;

    async fn pull(&self, config: &Self::Config) -> anyhow::Result<()> {
        let image = config.image().to_owned();
        let is_content_trust_enabled = false;

        if is_content_trust_enabled {
            log::info!("Pulling image via digest {}...", image);
        } else {
            log::info!("Pulling image via tag {}...", image);
        }

        let creds = match config.auth() {
            Some(a) => {
                let json = serde_json::to_string(&a).with_context(|| {
                    Error::RegistryOperation(RegistryOperation::PullImage(image.clone()))
                })?;
                let engine = base64::engine::general_purpose::URL_SAFE;
                base64::Engine::encode(&engine, &json)
            }
            None => String::new(),
        };

        self.client
            .image_create(&image, "", "", "", "", &creds, "")
            .await
            .context(Error::Docker)
            .map_err(|e| {
                log::warn!("{:?}", e);
                e
            })
            .with_context(|| {
                Error::RegistryOperation(RegistryOperation::PullImage(image.clone()))
            })?;

        log::info!("Successfully pulled image {}", image);

        // Now, get the image_id of the image we just pulled for image garbage collection in future
        match self.list_images().await {
            Ok(image_name_to_id) => {
                if image_name_to_id.is_empty() {
                    log::error!("No docker images present on device: {} was just pulled, but not found on device", image);
                } else if let Some(image_id) = image_name_to_id.get(config.image()) {
                    self.image_use_data.record_image_use_timestamp(image_id)?;
                } else {
                    log::warn!("Could not retrieve image id. {} was not added to image garbage collection list and will not be garbage collected", image);
                }
            }
            Err(e) => log::error!("Could not get list of docker images: {}", e),
        };

        Ok(())
    }

    async fn remove(&self, name: &str) -> anyhow::Result<()> {
        log::info!("Removing image {}...", name);

        ensure_not_empty(name).with_context(|| {
            Error::RegistryOperation(RegistryOperation::RemoveImage(name.to_string()))
        })?;

        self.client
            .image_delete(name, true, false)
            .await
            .context(Error::Docker)
            .map_err(|e| {
                log::warn!("{:?}", e);
                e
            })
            .with_context(|| {
                Error::RegistryOperation(RegistryOperation::RemoveImage(name.to_string()))
            })?;

        log::info!("Successfully removed image {}", name);
        Ok(())
    }
}

#[async_trait::async_trait]
impl MakeModuleRuntime for DockerModuleRuntime<Connector> {
    type Config = DockerConfig;
    type Settings = Settings;
    type ModuleRuntime = Self;

    async fn make_runtime(
        settings: &Settings,
        create_socket_channel: UnboundedSender<ModuleAction>,
        image_use_data: ImagePruneData,
    ) -> anyhow::Result<Self::ModuleRuntime> {
        log::info!("Initializing module runtime...");

        let client = init_client(settings.moby_runtime().uri())?;
        create_network_if_missing(settings, &client).await?;

        // to avoid excessive FD usage, we will not allow sysinfo to keep files open.
        sysinfo::set_open_files_limit(0);
        let system_resources = System::new_all();
        log::info!("Successfully initialized module runtime");

        let runtime = Self {
            client,
            system_resources: Arc::new(Mutex::new(system_resources)),
            create_socket_channel,
            allow_elevated_docker_permissions: settings.allow_elevated_docker_permissions(),
            additional_info: settings.additional_info().clone(),
            image_use_data,
        };

        Ok(runtime)
    }
}

pub fn init_client(docker_url: &Url) -> anyhow::Result<DockerApiClient<Connector>> {
    // build the hyper client
    let connector = Connector::new(docker_url).context(Error::Initialization)?;

    // extract base path - the bit that comes after the scheme
    let base_path = docker_url
        .to_base_path()
        .context(Error::Initialization)?
        .to_str()
        .ok_or(Error::Initialization)?
        .to_string();

    let configuration = Configuration {
        base_path,
        uri_composer: Box::new(|base_path, path| {
            // https://docs.rs/hyperlocal/0.6.0/src/hyperlocal/lib.rs.html#59
            let host = hex::encode(base_path.as_bytes());
            let host_str = format!("unix://{}:0{}", host, path);
            Ok(host_str.parse()?)
        }),
        ..Default::default()
    };

    Ok(DockerApiClient::new(connector).with_configuration(configuration))
}

async fn create_network_if_missing(
    settings: &Settings,
    client: &DockerApiClient<Connector>,
) -> anyhow::Result<()> {
    let (enable_i_pv6, ipam) = get_ipv6_settings(settings.moby_runtime().network());
    let network_id = settings.moby_runtime().network().name();
    log::info!("Using runtime network id {}", network_id);

    let filter = format!(r#"{{"name":{{"{}":true}}}}"#, network_id);
    let existing_iotedge_networks = client
        .network_list(&filter)
        .await
        .context(Error::Docker)
        .map_err(|e| {
            log::warn!("{:?}", e);
            e
        })
        .context(Error::RuntimeOperation(RuntimeOperation::Init))?;

    if existing_iotedge_networks.is_empty() {
        let mut network_config =
            NetworkConfig::new(network_id.to_string()).with_enable_i_pv6(enable_i_pv6);

        if let Some(ipam_config) = ipam {
            network_config.set_IPAM(ipam_config);
        };

        client
            .network_create(network_config)
            .await
            .context(Error::Docker)
            .map_err(|e| {
                log::warn!("{:?}", e);
                e
            })
            .context(Error::RuntimeOperation(RuntimeOperation::Init))?;
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
impl<C> ModuleRuntime for DockerModuleRuntime<C>
where
    C: Clone + hyper::client::connect::Connect + Send + Sync + 'static,
{
    type Config = DockerConfig;
    type Module = DockerModule<C>;
    type ModuleRegistry = Self;

    async fn create(&self, mut module: ModuleSpec<Self::Config>) -> anyhow::Result<()> {
        log::info!("Creating module {}...", module.name());

        // we only want "docker" modules
        if module.r#type() != DOCKER_MODULE_TYPE {
            return Err(Error::InvalidModuleType(module.r#type().to_string()).into());
        }

        unset_privileged(
            self.allow_elevated_docker_permissions,
            module.config_mut().create_options_mut(),
        );
        drop_unsafe_privileges(
            self.allow_elevated_docker_permissions,
            module.config_mut().create_options_mut(),
        );

        let image = module.config().image().to_owned();
        let is_content_trust_enabled = false;

        if is_content_trust_enabled {
            log::info!("Creating image via digest {}...", &image);
        } else {
            log::info!("Creating image via tag {}...", &image);
        }

        let create_options = module.config().create_options().clone();
        let merged_env = merge_env(create_options.env(), module.env());

        let mut labels = create_options.labels().cloned().unwrap_or_default();
        labels.insert(OWNER_LABEL_KEY.to_string(), OWNER_LABEL_VALUE.to_string());
        labels.insert(
            ORIGINAL_IMAGE_LABEL_KEY.to_string(),
            module.config().image().to_string(),
        );

        log::debug!("Creating container {} with image {}", module.name(), image);

        let create_options = create_options
            .with_image(image)
            .with_env(merged_env)
            .with_labels(labels);

        // Here we don't add the container to the iot edge docker network as the edge-agent is expected to do that.
        // It contains the logic to add a container to the iot edge network only if a network is not already specified.
        self.client
            .container_create(module.name(), create_options)
            .await
            .context(Error::Docker)
            .map_err(|e| {
                log::warn!("{:?}", e);
                e
            })
            .with_context(|| {
                Error::RuntimeOperation(RuntimeOperation::CreateModule(module.name().to_string()))
            })?;

        // Now, get the image id of the image associated with the module we started
        let module_with_details = self.get(module.name()).await?;

        // update image use timestamp for image garbage collection job later
        self.image_use_data.record_image_use_timestamp(
            module_with_details
                .0
                .config()
                .image_hash()
                .ok_or(Error::GetImageId())?,
        )?;

        Ok(())
    }

    async fn get(&self, id: &str) -> anyhow::Result<(Self::Module, ModuleRuntimeState)> {
        log::debug!("Getting module {}...", id);

        ensure_not_empty(id)
            .with_context(|| Error::RuntimeOperation(RuntimeOperation::GetModule(id.to_owned())))?;

        let response = self
            .client
            .container_inspect(id, false)
            .await
            .context(Error::Docker)
            .with_context(|| Error::RuntimeOperation(RuntimeOperation::GetModule(id.to_owned())))?;

        let name = response
            .name()
            .ok_or_else(|| Error::RuntimeOperation(RuntimeOperation::GetModule(id.to_owned())))?;
        let name = name.trim_start_matches('/').to_owned();

        let mut create_options = ContainerCreateBody::new();
        let mut image = name.clone();

        if let Some(config) = response.config() {
            if let Some(labels) = config.labels() {
                // Conversion of HashMap to BTreeMap.
                let mut btree_labels = std::collections::BTreeMap::new();

                for (key, value) in labels {
                    btree_labels.insert(key.clone(), value.clone());

                    if key == "net.azure-devices.edge.original-image" {
                        image = value.clone();
                    }
                }

                create_options.set_labels(btree_labels);
            }
        }

        let mut config = DockerConfig::new(
            image,
            create_options,
            None,
            None,
            self.allow_elevated_docker_permissions,
        )
        .map_err(|_| Error::RuntimeOperation(RuntimeOperation::GetModule(id.to_string())))?;

        if let Some(image_hash) = response.image() {
            config = config.with_image_hash(image_hash.to_string());
        }

        let module = DockerModule::new(self.client.clone(), name, config).with_context(|| {
            Error::RuntimeOperation(RuntimeOperation::GetModule(id.to_string()))
        })?;
        let state = runtime_state(response.id(), response.state());

        Ok((module, state))
    }

    async fn start(&self, id: &str) -> anyhow::Result<()> {
        log::info!("Starting module {}...", id);

        ensure_not_empty(id).with_context(|| {
            Error::RuntimeOperation(RuntimeOperation::StartModule(id.to_owned()))
        })?;

        let (sender, receiver) = tokio::sync::oneshot::channel::<()>();

        self.create_socket_channel
            .send(ModuleAction::Start(id.to_string(), sender))
            .map_err(|_| {
                log::error!("Could not notify workload manager, start of module: {}", id);
                Error::RuntimeOperation(RuntimeOperation::StartModule(id.to_string()))
            })?;

        receiver.await.map_err(|_| {
            log::error!(
                "Could not wait on workload manager response, start of module: {}",
                id
            );
            Error::RuntimeOperation(RuntimeOperation::StartModule(id.to_owned()))
        })?;

        self.client
            .container_start(id, "")
            .await
            .context(Error::Docker)
            .map_err(|e| {
                log::warn!("{:?}", e);
                e
            })
            .with_context(|| Error::RuntimeOperation(RuntimeOperation::StartModule(id.to_owned())))
    }

    async fn stop(&self, id: &str, wait_before_kill: Option<Duration>) -> anyhow::Result<()> {
        log::info!("Stopping module {}...", id);

        ensure_not_empty(id).with_context(|| {
            Error::RuntimeOperation(RuntimeOperation::StopModule(id.to_owned()))
        })?;

        #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
        let wait_timeout = wait_before_kill.map(|s| match s.as_secs() {
            s if s > i32::max_value() as u64 => i32::max_value(),
            s => s as i32,
        });

        self.create_socket_channel
            .send(ModuleAction::Stop(id.to_string()))
            .map_err(|_| {
                log::error!("Could not notify workload manager, stop of module: {}", id);
                Error::RuntimeOperation(RuntimeOperation::GetModule(id.to_string()))
            })?;

        self.client
            .container_stop(id, wait_timeout)
            .await
            .context(Error::Docker)
            .map_err(|e| {
                log::warn!("{:?}", e);
                e
            })
            .with_context(|| Error::RuntimeOperation(RuntimeOperation::StopModule(id.to_owned())))
    }

    async fn restart(&self, id: &str) -> anyhow::Result<()> {
        log::info!("Restarting module {}...", id);
        ensure_not_empty(id).with_context(|| {
            Error::RuntimeOperation(RuntimeOperation::RestartModule(id.to_owned()))
        })?;

        self.client
            .container_restart(id, None)
            .await
            .context(Error::Docker)
            .map_err(|e| {
                log::warn!("{:?}", e);
                e
            })
            .with_context(|| {
                Error::RuntimeOperation(RuntimeOperation::RestartModule(id.to_owned()))
            })
    }

    async fn remove(&self, id: &str) -> anyhow::Result<()> {
        // get the image id of the image associated with the module we want to delete
        let module_with_details = self.get(id).await?;
        let image_id = module_with_details
            .0
            .config()
            .image_hash()
            .ok_or(Error::GetImageId())?;

        log::info!("Removing module {}...", id);

        ensure_not_empty(id).with_context(|| {
            Error::RuntimeOperation(RuntimeOperation::RemoveModule(id.to_owned()))
        })?;

        self.client
            .container_delete(
                id, /* remove volumes */ false, /* force */ true,
                /* remove link */ false,
            )
            .await
            .context(Error::Docker)
            .map_err(|e| {
                log::warn!("{:?}", e);
                e
            })
            .with_context(|| {
                Error::RuntimeOperation(RuntimeOperation::RemoveModule(id.to_owned()))
            })?;

        // update image use timestamp for image garbage collection job later
        self.image_use_data.record_image_use_timestamp(image_id)?;

        // Remove the socket to avoid having socket files polluting the home folder.
        self.create_socket_channel
            .send(ModuleAction::Remove(id.to_string()))
            .map_err(|_| {
                log::error!(
                    "Could not notify workload manager, remove of module: {}",
                    id
                );
                anyhow::anyhow!(Error::RuntimeOperation(RuntimeOperation::GetModule(
                    id.to_string()
                )))
            })
    }

    async fn system_info(&self) -> anyhow::Result<CoreSystemInfo> {
        log::info!("Querying system info...");

        let total_memory = {
            let mut system_resources = self.system_resources.as_ref().lock().await;
            system_resources.refresh_memory();
            total_memory_bytes(&system_resources)
        };

        let mut system_info = CoreSystemInfo::default();

        let docker_info = self
            .client
            .system_info()
            .await
            .context(Error::Docker)
            .context(Error::RuntimeOperation(RuntimeOperation::SystemInfo))?;
        system_info.server_version = docker_info.server_version().map(ToOwned::to_owned);
        system_info.total_memory = Some(total_memory);
        system_info.merge_additional(self.additional_info.clone());

        log::info!("Successfully queried system info");
        Ok(system_info)
    }

    async fn system_resources(&self) -> anyhow::Result<SystemResources> {
        log::info!("Querying system resources...");

        let uptime = nix::sys::sysinfo::sysinfo()?.uptime().as_secs();

        // Get system resources
        let mut system_resources = self.system_resources.as_ref().lock().await;
        system_resources.refresh_all();

        let start_time = system_resources
            .process(sysinfo::Pid::from_u32(process::id()))
            .map(ProcessExt::start_time)
            .unwrap_or_default();

        let current_time = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap_or_default()
            .as_secs();

        let used_cpu = system_resources.global_cpu_info().cpu_usage();
        let total_memory = total_memory_bytes(&system_resources);
        let used_memory = used_memory_bytes(&system_resources);

        let disks = system_resources
            .disks()
            .iter()
            .map(|disk| {
                DiskInfo::new(
                    disk.name().to_string_lossy().into_owned(),
                    disk.available_space(),
                    disk.total_space(),
                    String::from_utf8_lossy(disk.file_system()).into_owned(),
                    format!("{:?}", disk.type_()),
                )
            })
            .collect();

        // Get Docker Stats
        // Note a for_each loop is used for simplicity with async operations
        // While a stream could be used for parallel operations, it isn't necessary here
        let modules = self.list().await?;
        let mut docker_stats = Vec::with_capacity(modules.len());
        for module in modules {
            let stats = self
                .client
                .container_stats(module.name(), false)
                .await
                .context(Error::Docker)?;

            docker_stats.push(stats);
        }
        let docker_stats = serde_json::to_string(&docker_stats)
            .map_err(|_| Error::RuntimeOperation(RuntimeOperation::SystemResources))?;

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

    async fn list(&self) -> anyhow::Result<Vec<Self::Module>> {
        log::debug!("Listing modules...");

        let mut filters = HashMap::new();
        filters.insert("label", LABELS);
        let filters = serde_json::to_string(&filters)
            .context(Error::RuntimeOperation(RuntimeOperation::ListModules))?;

        let containers = self
            .client
            .container_list(
                true,  /*all*/
                0,     /*limit*/
                false, /*size*/
                &filters,
            )
            .await
            .context(Error::Docker)
            .context(Error::RuntimeOperation(RuntimeOperation::ListModules))?;

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
                    self.allow_elevated_docker_permissions,
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

    async fn list_with_details(&self) -> anyhow::Result<Vec<(Self::Module, ModuleRuntimeState)>> {
        let mut result = Vec::new();
        for module in self.list().await? {
            // Note, if error calling just drop module from list
            match self.get(module.name()).await {
                Ok(module_with_details) => {
                    result.push(module_with_details);
                }
                Err(err) => {
                    log::warn!(
                        "error when getting details for {}: {:?}",
                        module.name(),
                        err
                    );
                }
            }
        }

        Ok(result)
    }

    async fn list_images(&self) -> anyhow::Result<HashMap<String, String>> {
        let images = self
            .client
            .images_list(false, "", false)
            .await
            .context(Error::Docker)
            .map_err(|e| {
                log::warn!("{:?}", e);
                e
            })
            .context(Error::RuntimeOperation(RuntimeOperation::ListImages))?;

        let mut result: HashMap<String, String> = HashMap::new();
        for image in images {
            // an individual image id may be associated with multiple image names
            for name in image.repo_tags() {
                result.insert(name.clone(), image.id().clone());
            }
        }

        Ok(result)
    }

    async fn logs(&self, id: &str, options: &LogOptions) -> anyhow::Result<hyper::Body> {
        log::info!("Getting logs for module {}...", id);

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
            .context(Error::Docker)
            .map_err(|e| {
                log::warn!("{:?}", e);
                e
            })
    }

    async fn remove_all(&self) -> anyhow::Result<()> {
        let modules = self.list().await?;
        let mut remove = vec![];

        for module in &modules {
            remove.push(ModuleRuntime::remove(self, module.name()));
        }

        for result in futures::future::join_all(remove).await {
            if let Err(err) = result {
                log::warn!("Failed to remove module: {:?}", err);
            }
        }

        Ok(())
    }

    async fn stop_all(&self, wait_before_kill: Option<Duration>) -> anyhow::Result<()> {
        let modules = self.list().await?;
        let mut stop = vec![];

        for module in &modules {
            stop.push(self.stop(module.name(), wait_before_kill));
        }

        for result in futures::future::join_all(stop).await {
            if let Err(err) = result {
                log::warn!("Failed to stop module: {:?}", err);
            }
        }

        Ok(())
    }

    async fn module_top(&self, id: &str) -> anyhow::Result<Vec<i32>> {
        let top_response = self
            .client
            .container_top(id, "")
            .await
            .context(Error::Docker)
            .map_err(|e| {
                log::warn!("{:?}", e);
                e
            })
            .with_context(|| Error::RuntimeOperation(RuntimeOperation::TopModule(id.to_owned())))?;

        let pids = parse_top_response::<Deserializer>(&top_response)
            .with_context(|| Error::RuntimeOperation(RuntimeOperation::TopModule(id.to_owned())))?;

        Ok(pids)
    }

    fn registry(&self) -> &Self::ModuleRegistry {
        self
    }

    fn error_code(error: &anyhow::Error) -> hyper::StatusCode {
        if let Some(error) = error.root_cause().downcast_ref::<docker::apis::ApiError>() {
            error.code
        } else {
            hyper::StatusCode::INTERNAL_SERVER_ERROR
        }
    }
}

fn total_memory_bytes(system_resources: &System) -> u64 {
    system_resources.total_memory()
}

fn used_memory_bytes(system_resources: &System) -> u64 {
    system_resources.used_memory()
}

fn parse_top_response<'de, D>(resp: &InlineResponse2001) -> Result<Vec<i32>, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let titles = resp
        .titles()
        .ok_or_else(|| serde::de::Error::missing_field("Titles"))?;
    let pid_index = titles
        .iter()
        .position(|s| s.as_str() == "PID")
        .ok_or_else(|| {
            serde::de::Error::invalid_value(
                serde::de::Unexpected::Seq,
                &"array including the column title 'PID'",
            )
        })?;
    let processes = resp
        .processes()
        .ok_or_else(|| serde::de::Error::missing_field("Processes"))?;
    let pids = processes
        .iter()
        .map(|p| {
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
        .collect::<Result<_, _>>();

    pids
}

// Disallow adding privileged and other capabilities if allow_elevated_docker_permissions is false
fn unset_privileged(
    allow_elevated_docker_permissions: bool,
    create_options: &mut ContainerCreateBody,
) {
    if allow_elevated_docker_permissions {
        return;
    }
    if let Some(config) = create_options.host_config() {
        if config.privileged() == Some(&true) || config.cap_add().map_or(0, Vec::len) != 0 {
            log::warn!("Privileged capabilities are disallowed on this device. Privileged capabilities can be used to gain root access. If a module needs to run as privileged, and you are aware of the consequences, set `allow_elevated_docker_permissions` to `true` in the config.toml and restart the service.");
            let mut config = config.clone();

            config.set_privileged(false);
            config.reset_cap_add();

            create_options.set_host_config(config);
        }
    }
}

fn drop_unsafe_privileges(
    allow_elevated_docker_permissions: bool,
    create_options: &mut ContainerCreateBody,
) {
    // Don't change default behavior unless privileged containers are disallowed
    if allow_elevated_docker_permissions {
        return;
    }

    // These capabilities are provided by default and can be used to gain root access:
    // https://labs.f-secure.com/blog/helping-root-out-of-the-container/
    // They must be explicitly enabled
    let mut caps_to_drop = vec!["CHOWN".to_owned(), "SETUID".to_owned()];

    // The suggested `Option::map_or_else` requires cloning `caps_to_drop`.
    #[allow(clippy::option_if_let_else)]
    let host_config = if let Some(config) = create_options.host_config() {
        // Don't drop caps that the user added explicitly
        if let Some(cap_add) = config.cap_add() {
            caps_to_drop.retain(|cap_drop| {
                !(cap_add.contains(cap_drop) || cap_add.contains(&format!("CAP_{}", cap_drop)))
            });
        }
        // Add customer specified cap_drops
        if let Some(cap_drop) = config.cap_drop() {
            caps_to_drop.extend_from_slice(cap_drop);
        }

        config.clone().with_cap_drop(caps_to_drop)
    } else {
        HostConfig::new().with_cap_drop(caps_to_drop)
    };

    create_options.set_host_config(host_config);
}

#[cfg(test)]
mod tests {
    use std::process::{Command, Stdio};

    use super::*;

    #[test]
    fn parse_top_response_returns_pid_array() {
        let response = InlineResponse2001::new()
            .with_titles(vec!["PID".to_string()])
            .with_processes(vec![vec!["123".to_string()]]);

        let pids = parse_top_response::<Deserializer>(&response);

        assert_eq!(vec![123], pids.unwrap());
    }

    #[test]
    fn parse_top_response_returns_error_when_titles_is_missing() {
        let response = InlineResponse2001::new().with_processes(vec![vec!["123".to_string()]]);

        let pids = parse_top_response::<Deserializer>(&response);

        assert_eq!("missing field `Titles`", format!("{}", pids.unwrap_err()));
    }

    #[test]
    fn parse_top_response_returns_error_when_pid_title_is_missing() {
        let response = InlineResponse2001::new().with_titles(vec!["Command".to_string()]);

        let pids = parse_top_response::<Deserializer>(&response);

        assert_eq!(
            "invalid value: sequence, expected array including the column title 'PID'",
            format!("{}", pids.unwrap_err())
        );
    }

    #[test]
    fn parse_top_response_returns_error_when_processes_is_missing() {
        let response = InlineResponse2001::new().with_titles(vec!["PID".to_string()]);

        let pids = parse_top_response::<Deserializer>(&response);

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

        assert_eq!(
            "invalid value: string \"xyz\", expected a process ID number",
            format!("{}", pids.unwrap_err())
        );
    }

    #[test]
    fn unset_privileged_works() {
        let mut create_options =
            ContainerCreateBody::new().with_host_config(HostConfig::new().with_privileged(true));

        // Doesn't remove privileged
        unset_privileged(true, &mut create_options);
        assert!(create_options.host_config().unwrap().privileged().unwrap());
        // Removes privileged
        unset_privileged(false, &mut create_options);
        assert!(!create_options.host_config().unwrap().privileged().unwrap());
        create_options.set_host_config(
            HostConfig::new().with_cap_add(vec!["CAP1".to_owned(), "CAP2".to_owned()]),
        );

        // Doesn't remove caps
        unset_privileged(true, &mut create_options);
        assert_eq!(
            create_options.host_config().unwrap().cap_add(),
            Some(&vec!["CAP1".to_owned(), "CAP2".to_owned()])
        );

        // Removes caps
        unset_privileged(false, &mut create_options);
        assert_eq!(create_options.host_config().unwrap().cap_add(), None);
    }

    #[test]
    fn drop_unsafe_privileges_works() {
        let mut create_options = ContainerCreateBody::new().with_host_config(HostConfig::new());
        // Do nothing if privileged is allowed
        drop_unsafe_privileges(true, &mut create_options);
        assert_eq!(create_options.host_config().unwrap().cap_drop(), None);
        // Drops privileges by if privileged is false
        drop_unsafe_privileges(false, &mut create_options);
        assert_eq!(
            create_options.host_config().unwrap().cap_drop(),
            Some(&vec!["CHOWN".to_owned(), "SETUID".to_owned()])
        );
        // Doesn't drop caps if specified
        create_options
            .set_host_config(HostConfig::new().with_cap_add(vec!["CAP_CHOWN".to_owned()]));
        drop_unsafe_privileges(false, &mut create_options);
        assert_eq!(
            create_options.host_config().unwrap().cap_drop(),
            Some(&vec!["SETUID".to_owned()])
        );

        // Doesn't drop caps if specified without CAP_
        create_options.set_host_config(HostConfig::new().with_cap_add(vec!["CHOWN".to_owned()]));
        drop_unsafe_privileges(false, &mut create_options);
        assert_eq!(
            create_options.host_config().unwrap().cap_drop(),
            Some(&vec!["SETUID".to_owned()])
        );
    }

    // Compare the total memory returned by the 'total_memory_bytes()' helper method
    // to the value in /proc/meminfo
    #[test]
    fn test_total_memory_bytes() {
        // Use 'total_memory_bytes()' helper method to get total memory
        let system_resources = System::new_all();
        let total_memory_bytes = total_memory_bytes(&system_resources);

        // Get expected total memory directly from /proc/meminfo
        let cat_proc_meminfo = Command::new("cat")
            .arg("/proc/meminfo")
            .stdout(Stdio::piped())
            .spawn()
            .expect("Failed to execute 'cat /proc/meminfo'");
        let grep_memtotal = Command::new("grep")
            .arg("-i")
            .arg("memtotal")
            .stdin(Stdio::from(cat_proc_meminfo.stdout.unwrap()))
            .stdout(Stdio::piped())
            .spawn()
            .expect("Failed to execute 'grep -i memtotal'");
        let grep_value = Command::new("grep")
            .arg("-o")
            .arg("[0-9]*")
            .stdin(Stdio::from(grep_memtotal.stdout.unwrap()))
            .stdout(Stdio::piped())
            .spawn()
            .expect("Failed to execute 'grep -o [0-9]*'");
        let output = grep_value.wait_with_output().unwrap();
        let expected_total_memory_kilobytes_str = str::from_utf8(&output.stdout).unwrap().trim();
        let expected_total_memory_bytes =
            expected_total_memory_kilobytes_str.parse::<u64>().unwrap() * 1024;

        // Compare
        assert_eq!(total_memory_bytes, expected_total_memory_bytes);
    }
}
