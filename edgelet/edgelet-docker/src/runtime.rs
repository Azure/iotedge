// Copyright (c) Microsoft. All rights reserved.

use opentelemetry::{
    global,
    trace::{FutureExt, Span, TraceContextExt, Tracer, TracerProvider},
    Context,
};
use std::collections::{BTreeMap, HashMap};
use std::convert::TryInto;
use std::path::{Path, PathBuf};
use std::sync::Arc;
use std::time::{Duration, SystemTime, UNIX_EPOCH};
use std::{mem, process, str};

use edgelet_core::module::ModuleAction;
use failure::ResultExt;
use hyper::{Body, Client, Uri};
use log::{debug, error, info, warn, Level};
use sysinfo::{DiskExt, ProcessExt, ProcessorExt, System, SystemExt};
use tokio::sync::Mutex;
use url::Url;

use docker::apis::{Configuration, DockerApi, DockerApiClient};
use docker::models::{ContainerCreateBody, HostConfig, InlineResponse2001, Ipam, NetworkConfig};
use edgelet_core::{
    DiskInfo, LogOptions, MakeModuleRuntime, Module, ModuleRegistry, ModuleRuntime,
    ModuleRuntimeState, ProvisioningInfo, RegistryOperation, RuntimeOperation,
    SystemInfo as CoreSystemInfo, SystemResources, UrlExt,
};
use edgelet_settings::{
    ContentTrust, DockerConfig, Ipam as CoreIpam, MobyNetwork, ModuleSpec, RuntimeSettings,
    Settings,
};
use edgelet_utils::{ensure_not_empty_with_context, log_failure};
use http_common::Connector;
use tokio::sync::mpsc::UnboundedSender;

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
    create_socket_channel: UnboundedSender<ModuleAction>,
    allow_elevated_docker_permissions: bool,
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
                1,
            );

            let mut notary_registries = BTreeMap::new();
            for (registry_server_hostname, cert_id) in content_trust_map {
                let cert_buf = cert_client.get_cert(cert_id).await.map_err(|_| {
                    ErrorKind::NotaryRootCAReadError("Notary root CA read error".to_owned())
                })?;

                let config_path =
                    notary::notary_init(&home_dir, registry_server_hostname, &cert_buf).map_err(
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

        Some((notary_auth, gun, tag, config_path.clone()))
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
                let json = ResultExt::with_context(serde_json::to_string(&a), |_| {
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

    async fn make_runtime(
        settings: &Settings,
        create_socket_channel: UnboundedSender<ModuleAction>,
    ) -> Result<Self::ModuleRuntime> {
        info!("Initializing module runtime...");

        let client = init_client(settings.moby_runtime().uri())?;
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
            create_socket_channel,
            allow_elevated_docker_permissions: settings.allow_elevated_docker_permissions(),
        };

        Ok(runtime)
    }
}

pub fn init_client(docker_url: &Url) -> Result<DockerApiClient> {
    // build the hyper client
    let client: Client<_, Body> = Connector::new(docker_url)
        .map_err(|e| Error::from(ErrorKind::Initialization(e.to_string())))?
        .into_client();

    // extract base path - the bit that comes after the scheme
    let base_path = docker_url
        .to_base_path()
        .context(ErrorKind::Initialization("".to_owned()))?
        .to_str()
        .ok_or_else(|| ErrorKind::Initialization("".to_owned()))?
        .to_string();
    let uri_composer = Box::new(|base_path: &str, path: &str| {
        // https://docs.rs/hyperlocal/0.6.0/src/hyperlocal/lib.rs.html#59
        let host = hex::encode(base_path.as_bytes());
        let host_str = format!("unix://{}:0{}", host, path);
        let result: Uri = host_str.parse()?;

        Ok(result)
    });

    let configuration = Configuration {
        base_path,
        uri_composer,
        ..Default::default()
    };

    Ok(DockerApiClient::new(client).with_configuration(configuration))
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

    async fn create(&self, mut module: ModuleSpec<Self::Config>) -> Result<()> {
        info!("Creating module {}...", module.name());

        // we only want "docker" modules
        if module.r#type() != DOCKER_MODULE_TYPE {
            return Err(Error::from(ErrorKind::InvalidModuleType(
                module.r#type().to_string(),
            )));
        }

        unset_privileged(
            self.allow_elevated_docker_permissions,
            &mut module.config_mut().create_options_mut(),
        );
        drop_unsafe_privileges(
            self.allow_elevated_docker_permissions,
            &mut module.config_mut().create_options_mut(),
        );

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
        let tracer_provider = global::tracer_provider();
        let tracer = tracer_provider.tracer("aziot-edged", Some(env!("CARGO_PKG_VERSION")));
        let mut span = tracer.start("DockerModuleRuntime:get");
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

        let name = response.name().ok_or_else(|| {
            ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(id.to_string()))
        })?;
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
        .map_err(|_| ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(id.to_string())))?;

        if let Some(image_hash) = response.image() {
            config = config.with_image_hash(image_hash.to_string());
        }

        let module =
            ResultExt::with_context(DockerModule::new(self.client.clone(), name, config), |_| {
                ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(id.to_string()))
            })?;
        let state = runtime_state(response.id(), response.state());
        span.end();
        Ok((module, state))
    }

    async fn start(&self, id: &str) -> Result<()> {
        info!("Starting module {}...", id);

        ensure_not_empty_with_context(id, || {
            ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(id.to_owned()))
        })
        .map_err(Error::from)?;

        let (sender, receiver) = tokio::sync::oneshot::channel::<()>();

        self.create_socket_channel
            .send(ModuleAction::Start(id.to_string(), sender))
            .map_err(|_| {
                error!("Could not notify workload manager, start of module: {}", id);
                Error::from(ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(
                    id.to_string(),
                )))
            })?;

        receiver.await.map_err(|_| {
            error!(
                "Could not wait on workload manager response, start of module: {}",
                id
            );
            Error::from(ErrorKind::RuntimeOperation(RuntimeOperation::StartModule(
                id.to_owned(),
            )))
        })?;

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

        self.create_socket_channel
            .send(ModuleAction::Stop(id.to_string()))
            .map_err(|_| {
                error!("Could not notify workload manager, stop of module: {}", id);
                Error::from(ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(
                    id.to_string(),
                )))
            })?;

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
                id, /* remove volumes */ false, /* force */ true,
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
            })?;

        // Remove the socket to avoid having socket files polluting the home folder.
        self.create_socket_channel
            .send(ModuleAction::Remove(id.to_string()))
            .map_err(|_| {
                error!(
                    "Could not notify workload manager, remove of module: {}",
                    id
                );
                Error::from(ErrorKind::RuntimeOperation(RuntimeOperation::GetModule(
                    id.to_string(),
                )))
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
        system_resources.refresh_all();

        let start_time = process::id()
            .try_into()
            .map(|id| {
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
        let tracer_provider = global::tracer_provider();
        let tracer = tracer_provider.tracer("aziot-edged", Some(env!("CARGO_PKG_VERSION")));
        let mut span = tracer.start("DockerModuleRuntime:list");
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
        span.end();
        Ok(result)
    }

    async fn list_with_details(&self) -> Result<Vec<(Self::Module, ModuleRuntimeState)>> {
        let tracer_provider = global::tracer_provider();
        let tracer = tracer_provider.tracer("aziot-edged", Some(env!("CARGO_PKG_VERSION")));
        let span = tracer.start("DockerModuleRuntime:list_with_details");
        let cx = Context::current_with_span(span);
        let mut result = Vec::new();
        for module in self.list().await? {
            // Note, if error calling just drop module from list
            if let Ok(module_with_details) =
                FutureExt::with_context(self.get(module.name()), cx.clone()).await
            {
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
        let modules = self.list().await?;
        let mut remove = vec![];

        for module in &modules {
            remove.push(ModuleRuntime::remove(self, module.name()));
        }

        for result in futures::future::join_all(remove).await {
            if let Err(err) = result {
                log::warn!("Failed to remove module: {}", err);
            }
        }

        Ok(())
    }

    async fn stop_all(&self, wait_before_kill: Option<Duration>) -> Result<()> {
        let modules = self.list().await?;
        let mut stop = vec![];

        for module in &modules {
            stop.push(self.stop(module.name(), wait_before_kill));
        }

        for result in futures::future::join_all(stop).await {
            if let Err(err) = result {
                log::warn!("Failed to stop module: {}", err);
            }
        }

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

        let pids =
            ResultExt::with_context(parse_top_response::<Deserializer>(&top_response), |_| {
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
    let pids: std::result::Result<_, _> = processes
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
        .collect();

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
            warn!("Privileged capabilities are disallowed on this device. Privileged capabilities can be used to gain root access. If a module needs to run as privileged, and you are aware of the consequences, set `allow_elevated_docker_permissions` to `true` in the config.toml and restart the service.");
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
    let mut caps_to_drop = vec!["CAP_CHOWN".to_owned(), "CAP_SETUID".to_owned()];

    // The suggested `Option::map_or_else` requires cloning `caps_to_drop`.
    #[allow(clippy::option_if_let_else)]
    let host_config = if let Some(config) = create_options.host_config() {
        // Don't drop caps that the user added explicitly
        if let Some(cap_add) = config.cap_add() {
            caps_to_drop.retain(|cap_drop| !cap_add.contains(cap_drop));
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
            Some(&vec!["CAP_CHOWN".to_owned(), "CAP_SETUID".to_owned()])
        );
        // Doesn't drop caps if specified
        create_options
            .set_host_config(HostConfig::new().with_cap_add(vec!["CAP_CHOWN".to_owned()]));
        drop_unsafe_privileges(false, &mut create_options);
        assert_eq!(
            create_options.host_config().unwrap().cap_drop(),
            Some(&vec!["CAP_SETUID".to_owned()])
        );
    }
}
