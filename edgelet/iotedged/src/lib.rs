// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::doc_markdown, // clippy want the "IoT" of "IoT Hub" in a code fence
    clippy::shadow_unrelated,
    clippy::stutter,
    clippy::use_self,
)]

extern crate base64;
#[macro_use]
extern crate clap;
extern crate config;
extern crate docker;
extern crate edgelet_core;
extern crate edgelet_docker;
extern crate edgelet_hsm;
extern crate edgelet_http;
extern crate edgelet_http_mgmt;
extern crate edgelet_http_workload;
extern crate edgelet_iothub;
#[cfg(test)]
extern crate edgelet_test_utils;
extern crate edgelet_utils;
extern crate env_logger;
extern crate failure;
extern crate futures;
extern crate hsm;
extern crate hyper;
extern crate iothubservice;
#[macro_use]
extern crate log;
extern crate provisioning;
extern crate serde;
extern crate sha2;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
#[cfg(test)]
extern crate tempdir;
extern crate tokio;
extern crate tokio_signal;
extern crate url;
extern crate url_serde;
#[cfg(target_os = "windows")]
#[macro_use]
extern crate windows_service;
#[cfg(target_os = "windows")]
extern crate win_logger;

pub mod app;
mod error;
pub mod logging;
pub mod settings;
pub mod signal;
pub mod workload;

#[cfg(not(target_os = "windows"))]
pub mod unix;

#[cfg(target_os = "windows")]
pub mod windows;

use std::collections::HashMap;
use std::env;
use std::fs;
use std::fs::{DirBuilder, File};
use std::io::Write;
use std::path::{Path, PathBuf};

use failure::{Fail, ResultExt};
use futures::future::Either;
use futures::sync::oneshot::{self, Receiver};
use futures::{future, Future};
use hyper::server::conn::Http;
use hyper::Uri;
use sha2::{Digest, Sha256};
use url::Url;

use docker::models::HostConfig;
use edgelet_core::crypto::{
    CreateCertificate, Decrypt, DerivedKeyStore, Encrypt, GetTrustBundle, KeyIdentity, KeyStore,
    MasterEncryptionKey, MemoryKey, MemoryKeyStore, Sign, IOTEDGED_CA_ALIAS,
};
use edgelet_core::watchdog::Watchdog;
use edgelet_core::WorkloadConfig;
use edgelet_core::{CertificateIssuer, CertificateProperties, CertificateType};
use edgelet_core::{ModuleRuntime, ModuleSpec};
use edgelet_docker::{DockerConfig, DockerModuleRuntime};
use edgelet_hsm::tpm::{TpmKey, TpmKeyStore};
use edgelet_hsm::Crypto;
use edgelet_http::client::{Client as HttpClient, ClientImpl};
use edgelet_http::logging::LoggingService;
use edgelet_http::{HyperExt, MaybeProxyClient, UrlExt, API_VERSION};
use edgelet_http_mgmt::ManagementService;
use edgelet_http_workload::WorkloadService;
use edgelet_iothub::{HubIdentityManager, SasTokenSource};
use hsm::tpm::Tpm;
use hsm::ManageTpmKeys;
use iothubservice::DeviceClient;
use provisioning::provisioning::{
    BackupProvisioning, DpsProvisioning, ManualProvisioning, Provision, ProvisioningResult,
};

use settings::{Dps, Manual, Provisioning, Settings, DEFAULT_CONNECTION_STRING};
use workload::WorkloadData;

pub use self::error::{Error, ErrorKind, InitializeErrorReason};

const EDGE_RUNTIME_MODULEID: &str = "$edgeAgent";
const EDGE_RUNTIME_MODULE_NAME: &str = "edgeAgent";
const AUTH_SCHEME: &str = "sasToken";

/// The following constants are all environment variables names injected into
/// the Edge Agent container.
///
/// This variable holds the host name of the IoT Hub instance that edge agent
/// is expected to work with.
const HOSTNAME_KEY: &str = "IOTEDGE_IOTHUBHOSTNAME";

/// This variable holds the host name for the edge device. This name is used
/// by the edge agent to provide the edge hub container an alias name in the
/// network so that TLS cert validation works.
const GATEWAY_HOSTNAME_KEY: &str = "EDGEDEVICEHOSTNAME";

/// This variable holds the IoT Hub device identifier.
const DEVICEID_KEY: &str = "IOTEDGE_DEVICEID";

/// This variable holds the IoT Hub module identifier.
const MODULEID_KEY: &str = "IOTEDGE_MODULEID";

/// This variable holds the URI to use for connecting to the workload endpoint
/// in iotedged. This is used by the edge agent to connect to the workload API
/// for its own needs and is also used for volume mounting into module
/// containers when the URI refers to a Unix domain socket.
const WORKLOAD_URI_KEY: &str = "IOTEDGE_WORKLOADURI";

/// This variable holds the URI to use for connecting to the management
/// endpoint in iotedged. This is used by the edge agent for managing module
/// lifetimes and module identities.
const MANAGEMENT_URI_KEY: &str = "IOTEDGE_MANAGEMENTURI";

/// This variable holds the authentication scheme that modules are to use when
/// connecting to other server modules (like Edge Hub). The authentication
/// scheme can mean either that we are to use SAS tokens or a TLS client cert.
const AUTHSCHEME_KEY: &str = "IOTEDGE_AUTHSCHEME";

/// This is the key for the edge runtime mode.
const EDGE_RUNTIME_MODE_KEY: &str = "Mode";

/// This is the edge runtime mode - it should always be iotedged, when iotedged starts edge runtime.
const EDGE_RUNTIME_MODE: &str = "iotedged";

/// The HSM lib expects this variable to be set with home directory of the daemon.
const HOMEDIR_KEY: &str = "IOTEDGE_HOMEDIR";

/// The HSM lib expects these environment variables to be set if the Edge has to be operated as a gateway
const DEVICE_CA_CERT_KEY: &str = "IOTEDGE_DEVICE_CA_CERT";
const DEVICE_CA_PK_KEY: &str = "IOTEDGE_DEVICE_CA_PK";
const TRUSTED_CA_CERTS_KEY: &str = "IOTEDGE_TRUSTED_CA_CERTS";

/// This is the key for the docker network Id.
const EDGE_NETWORKID_KEY: &str = "NetworkId";

/// This is the key for the largest API version that this edgelet supports
const API_VERSION_KEY: &str = "IOTEDGE_APIVERSION";

const IOTHUB_API_VERSION: &str = "2017-11-08-preview";
const UNIX_SCHEME: &str = "unix";

/// This is the name of the provisioning backup file
const EDGE_PROVISIONING_BACKUP_FILENAME: &str = "provisioning_backup.json";

/// This is the name of the settings backup file
const EDGE_SETTINGS_STATE_FILENAME: &str = "settings_state";

/// This is the name of the cache subdirectory for settings state
const EDGE_SETTINGS_SUBDIR: &str = "cache";

/// These are the properties of the workload CA certificate
const IOTEDGED_VALIDITY: u64 = 7_776_000; // 90 days
const IOTEDGED_COMMONNAME: &str = "iotedged workload ca";

const IOTEDGE_ID_CERT_MAX_DURATION_SECS: i64 = 7200; // 2 hours
const IOTEDGE_SERVER_CERT_MAX_DURATION_SECS: i64 = 7_776_000; // 90 days

pub struct Main {
    settings: Settings<DockerConfig>,
}

impl Main {
    pub fn new(settings: Settings<DockerConfig>) -> Self {
        Main { settings }
    }

    pub fn run_until<F>(self, shutdown_signal: F) -> Result<(), Error>
    where
        F: Future<Item = (), Error = ()> + Send + 'static,
    {
        let Main { settings } = self;

        let mut tokio_runtime = tokio::runtime::Runtime::new()
            .context(ErrorKind::Initialize(InitializeErrorReason::Tokio))?;

        if let Provisioning::Manual(ref manual) = settings.provisioning() {
            if manual.device_connection_string() == DEFAULT_CONNECTION_STRING {
                return Err(Error::from(ErrorKind::Initialize(
                    InitializeErrorReason::NotConfigured,
                )));
            }
        }

        let hyper_client = MaybeProxyClient::new(get_proxy_uri(None)?)
            .context(ErrorKind::Initialize(InitializeErrorReason::HttpClient))?;

        info!(
            "Using runtime network id {}",
            settings.moby_runtime().network()
        );
        let runtime = DockerModuleRuntime::new(settings.moby_runtime().uri())
            .context(ErrorKind::Initialize(InitializeErrorReason::ModuleRuntime))?
            .with_network_id(settings.moby_runtime().network().to_string());

        init_docker_runtime(&runtime, &mut tokio_runtime)?;

        info!(
            "Configuring {} as the home directory.",
            settings.homedir().display()
        );
        env::set_var(HOMEDIR_KEY, &settings.homedir());

        info!("Configuring certificates...");
        let certificates = &settings.certificates();
        match certificates.as_ref() {
            None => info!(
                "Transparent gateway certificates not found, operating in quick start mode..."
            ),
            Some(&c) => {
                let path = c.device_ca_cert().as_os_str();
                info!("Configuring the Device CA certificate using {:?}.", path);
                env::set_var(DEVICE_CA_CERT_KEY, path);

                let path = c.device_ca_pk().as_os_str();
                info!("Configuring the Device private key using {:?}.", path);
                env::set_var(DEVICE_CA_PK_KEY, path);

                let path = c.trusted_ca_certs().as_os_str();
                info!("Configuring the trusted CA certificates using {:?}.", path);
                env::set_var(TRUSTED_CA_CERTS_KEY, path);
            }
        };
        info!("Finished configuring certificates.");

        info!("Initializing hsm...");
        let crypto = Crypto::new().context(ErrorKind::Initialize(InitializeErrorReason::Hsm))?;
        info!("Finished initializing hsm.");

        // Detect if the settings were changed and if the device needs to be reconfigured
        let cache_subdir_path = Path::new(&settings.homedir()).join(EDGE_SETTINGS_SUBDIR);
        check_settings_state(
            cache_subdir_path.clone(),
            EDGE_SETTINGS_STATE_FILENAME,
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
        )?;

        info!("Provisioning edge device...");
        match settings.provisioning() {
            Provisioning::Manual(manual) => {
                let (key_store, provisioning_result, root_key) =
                    manual_provision(&manual, &mut tokio_runtime)?;
                info!("Finished provisioning edge device.");
                let cfg = WorkloadData::new(
                    provisioning_result.hub_name().to_string(),
                    provisioning_result.device_id().to_string(),
                    IOTEDGE_ID_CERT_MAX_DURATION_SECS,
                    IOTEDGE_SERVER_CERT_MAX_DURATION_SECS,
                );
                start_api(
                    &settings,
                    hyper_client,
                    &runtime,
                    &key_store,
                    cfg,
                    root_key,
                    shutdown_signal,
                    &crypto,
                    tokio_runtime,
                )?;
            }
            Provisioning::Dps(dps) => {
                let dps_path = cache_subdir_path.join(EDGE_PROVISIONING_BACKUP_FILENAME);
                let (key_store, provisioning_result, root_key, runtime) = dps_provision(
                    &dps,
                    hyper_client.clone(),
                    dps_path,
                    runtime,
                    &mut tokio_runtime,
                )?;
                info!("Finished provisioning edge device.");
                let cfg = WorkloadData::new(
                    provisioning_result.hub_name().to_string(),
                    provisioning_result.device_id().to_string(),
                    IOTEDGE_ID_CERT_MAX_DURATION_SECS,
                    IOTEDGE_SERVER_CERT_MAX_DURATION_SECS,
                );
                start_api(
                    &settings,
                    hyper_client,
                    &runtime,
                    &key_store,
                    cfg,
                    root_key,
                    shutdown_signal,
                    &crypto,
                    tokio_runtime,
                )?;
            }
        };

        info!("Shutdown complete.");
        Ok(())
    }
}

pub fn get_proxy_uri(https_proxy: Option<String>) -> Result<Option<Uri>, Error> {
    let proxy_uri = https_proxy
        .or_else(|| env::var("HTTPS_PROXY").ok())
        .or_else(|| env::var("https_proxy").ok());
    let proxy_uri = match proxy_uri {
        None => None,
        Some(s) => {
            let proxy = s.parse::<Uri>().context(ErrorKind::Initialize(
                InitializeErrorReason::InvalidProxyUri,
            ))?;

            // Mask the password in the proxy URI before logging it
            let mut sanitized_proxy = Url::parse(&proxy.to_string()).context(
                ErrorKind::Initialize(InitializeErrorReason::InvalidProxyUri),
            )?;
            if sanitized_proxy.password().is_some() {
                sanitized_proxy
                    .set_password(Some("******"))
                    .map_err(|()| ErrorKind::Initialize(InitializeErrorReason::InvalidProxyUri))?;
            }
            info!("Detected HTTPS proxy server {}", sanitized_proxy);

            Some(proxy)
        }
    };
    Ok(proxy_uri)
}

fn prepare_workload_ca<C>(crypto: &C) -> Result<(), Error>
where
    C: CreateCertificate,
{
    let edgelet_ca_props = CertificateProperties::new(
        IOTEDGED_VALIDITY,
        IOTEDGED_COMMONNAME.to_string(),
        CertificateType::Ca,
        IOTEDGED_CA_ALIAS.to_string(),
    )
    .with_issuer(CertificateIssuer::DeviceCa);

    crypto
        .create_certificate(&edgelet_ca_props)
        .context(ErrorKind::Initialize(
            InitializeErrorReason::PrepareWorkloadCa,
        ))?;
    Ok(())
}

fn destroy_workload_ca<C>(crypto: &C) -> Result<(), Error>
where
    C: CreateCertificate,
{
    crypto
        .destroy_certificate(IOTEDGED_CA_ALIAS.to_string())
        .context(ErrorKind::Initialize(
            InitializeErrorReason::DestroyWorkloadCa,
        ))?;
    Ok(())
}

fn check_settings_state<M, C>(
    subdir_path: PathBuf,
    filename: &str,
    settings: &Settings<DockerConfig>,
    runtime: &M,
    crypto: &C,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<(), Error>
where
    M: ModuleRuntime,
    <M as ModuleRuntime>::RemoveAllFuture: 'static,
    C: MasterEncryptionKey + CreateCertificate,
{
    info!("Detecting if configuration file has changed...");
    let path = subdir_path.join(filename);
    let mut reconfig_reqd = false;
    let diff = settings.diff_with_cached(path)?;
    if diff {
        info!("Change to configuration file detected.");
        reconfig_reqd = true;
    } else {
        info!("No change to configuration file detected.");

        #[allow(clippy::single_match_else)]
        match prepare_workload_ca(crypto) {
            Ok(()) => info!("Obtaining workload CA succeeded."),
            Err(_) => {
                reconfig_reqd = true;
                info!("Obtaining workload CA failed. Triggering reconfiguration");
            }
        };
    }
    if reconfig_reqd {
        reconfigure(
            subdir_path,
            filename,
            settings,
            runtime,
            crypto,
            tokio_runtime,
        )?;
    }
    Ok(())
}

fn reconfigure<M, C>(
    subdir: PathBuf,
    filename: &str,
    settings: &Settings<DockerConfig>,
    runtime: &M,
    crypto: &C,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<(), Error>
where
    M: ModuleRuntime,
    <M as ModuleRuntime>::RemoveAllFuture: 'static,
    C: MasterEncryptionKey + CreateCertificate,
{
    // Remove all edge containers and destroy the cache (settings and dps backup)
    info!("Removing all modules...");
    tokio_runtime
        .block_on(runtime.remove_all())
        .context(ErrorKind::Initialize(
            InitializeErrorReason::RemoveExistingModules,
        ))?;
    info!("Finished removing modules.");

    // Ignore errors from this operation because we could be recovering from a previous bad
    // configuration and shouldn't stall the current configuration because of that
    let _u = fs::remove_dir_all(subdir.clone());

    let path = subdir.join(filename);

    DirBuilder::new()
        .recursive(true)
        .create(subdir)
        .context(ErrorKind::Initialize(
            InitializeErrorReason::CreateSettingsDirectory,
        ))?;

    // Generate a new master encryption key and save the new settings
    crypto.create_key().context(ErrorKind::Initialize(
        InitializeErrorReason::CreateMasterEncryptionKey,
    ))?;
    // regenerate the workload CA certificate
    destroy_workload_ca(crypto)?;
    prepare_workload_ca(crypto)?;
    let mut file =
        File::create(path).context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;
    let s = serde_json::to_string(settings)
        .context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;
    let s = Sha256::digest_str(&s);
    let sb = base64::encode(&s);
    file.write_all(sb.as_bytes())
        .context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;

    Ok(())
}

#[allow(clippy::too_many_arguments)]
fn start_api<HC, K, F, C, W>(
    settings: &Settings<DockerConfig>,
    hyper_client: HC,
    runtime: &DockerModuleRuntime,
    key_store: &DerivedKeyStore<K>,
    workload_config: W,
    root_key: K,
    shutdown_signal: F,
    crypto: &C,
    mut tokio_runtime: tokio::runtime::Runtime,
) -> Result<(), Error>
where
    F: Future<Item = (), Error = ()> + Send + 'static,
    HC: ClientImpl + 'static,
    K: Sign + Clone + Send + Sync + 'static,
    C: CreateCertificate
        + Decrypt
        + Encrypt
        + GetTrustBundle
        + MasterEncryptionKey
        + Clone
        + Send
        + Sync
        + 'static,
    W: WorkloadConfig + Clone + Send + Sync + 'static,
{
    let hub_name = workload_config.iot_hub_name().to_string();
    let device_id = workload_config.device_id().to_string();
    let hostname = format!("https://{}", hub_name);
    let token_source = SasTokenSource::new(hub_name.clone(), device_id.clone(), root_key);
    let http_client = HttpClient::new(
        hyper_client,
        Some(token_source),
        IOTHUB_API_VERSION.to_string(),
        Url::parse(&hostname).context(ErrorKind::Initialize(InitializeErrorReason::HttpClient))?,
    )
    .context(ErrorKind::Initialize(InitializeErrorReason::HttpClient))?;
    let device_client = DeviceClient::new(http_client, device_id.clone())
        .context(ErrorKind::Initialize(InitializeErrorReason::DeviceClient))?;
    let id_man = HubIdentityManager::new(key_store.clone(), device_client);

    let (mgmt_tx, mgmt_rx) = oneshot::channel();
    let (work_tx, work_rx) = oneshot::channel();

    let mgmt = start_management(&settings, &runtime, &id_man, mgmt_rx);

    let workload = start_workload(
        &settings,
        key_store,
        &runtime,
        work_rx,
        crypto,
        workload_config,
    );

    let (runt_tx, runt_rx) = oneshot::channel();
    let edge_rt = start_runtime(&runtime, &id_man, &hub_name, &device_id, &settings, runt_rx)?;

    // Wait for the watchdog to finish, and then send signal to the workload and management services.
    // This way the edgeAgent can finish shutting down all modules.
    let edge_rt_with_cleanup = edge_rt.map_err(Into::into).and_then(|_| {
        mgmt_tx.send(()).unwrap_or(());
        work_tx.send(()).unwrap_or(());
        future::ok(())
    });

    let shutdown = shutdown_signal.map(move |_| {
        debug!("shutdown signaled");
        // Signal the watchdog to shutdown
        runt_tx.send(()).unwrap_or(());
    });
    tokio_runtime.spawn(shutdown);

    let services = mgmt
        .join3(workload, edge_rt_with_cleanup)
        .then(|result| match result {
            Ok(((), (), ())) => Ok(()),
            Err(err) => Err(err),
        });
    tokio_runtime.block_on(services)?;

    Ok(())
}

fn init_docker_runtime(
    runtime: &DockerModuleRuntime,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<(), Error> {
    info!("Initializing the module runtime...");
    tokio_runtime
        .block_on(runtime.init())
        .context(ErrorKind::Initialize(InitializeErrorReason::ModuleRuntime))?;
    info!("Finished initializing the module runtime.");
    Ok(())
}

fn manual_provision(
    provisioning: &Manual,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<(DerivedKeyStore<MemoryKey>, ProvisioningResult, MemoryKey), Error> {
    let manual = ManualProvisioning::new(provisioning.device_connection_string()).context(
        ErrorKind::Initialize(InitializeErrorReason::ManualProvisioningClient),
    )?;
    let memory_hsm = MemoryKeyStore::new();
    let provision = manual
        .provision(memory_hsm.clone())
        .map_err(|err| {
            Error::from(err.context(ErrorKind::Initialize(
                InitializeErrorReason::ManualProvisioningClient,
            )))
        })
        .and_then(move |prov_result| {
            memory_hsm
                .get(&KeyIdentity::Device, "primary")
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::Initialize(
                        InitializeErrorReason::ManualProvisioningClient,
                    )))
                })
                .and_then(|k| {
                    let derived_key_store = DerivedKeyStore::new(k.clone());
                    Ok((derived_key_store, prov_result, k))
                })
        });
    tokio_runtime.block_on(provision)
}

fn dps_provision<HC, M>(
    provisioning: &Dps,
    hyper_client: HC,
    backup_path: PathBuf,
    runtime: M,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<(DerivedKeyStore<TpmKey>, ProvisioningResult, TpmKey, M), Error>
where
    HC: 'static + ClientImpl,
    M: ModuleRuntime + Send + 'static,
{
    let tpm = Tpm::new().context(ErrorKind::Initialize(
        InitializeErrorReason::DpsProvisioningClient,
    ))?;
    let ek_result = tpm.get_ek().context(ErrorKind::Initialize(
        InitializeErrorReason::DpsProvisioningClient,
    ))?;
    let srk_result = tpm.get_srk().context(ErrorKind::Initialize(
        InitializeErrorReason::DpsProvisioningClient,
    ))?;
    let dps = DpsProvisioning::new(
        hyper_client,
        provisioning.global_endpoint().clone(),
        provisioning.scope_id().to_string(),
        provisioning.registration_id().to_string(),
        "2017-11-15".to_string(),
        ek_result,
        srk_result,
    )
    .context(ErrorKind::Initialize(
        InitializeErrorReason::DpsProvisioningClient,
    ))?;
    let tpm_hsm = TpmKeyStore::from_hsm(tpm).context(ErrorKind::Initialize(
        InitializeErrorReason::DpsProvisioningClient,
    ))?;
    let provision_with_file_backup = BackupProvisioning::new(dps, backup_path);
    let provision = provision_with_file_backup
        .provision(tpm_hsm.clone())
        .map_err(|err| {
            Error::from(err.context(ErrorKind::Initialize(
                InitializeErrorReason::DpsProvisioningClient,
            )))
        })
        .and_then(|prov_result| {
            if prov_result.reconfigure() {
                info!("Successful DPS provisioning. This will trigger reconfiguration of modules.");
                // Each time DPS provisions, it gets back a new device key. This results in obsolete
                // module keys in IoTHub from the previous provisioning. We delete all containers
                // after each DPS provisioning run so that IoTHub can be updated with new module
                // keys when the deployment is executed by EdgeAgent.
                let remove = runtime.remove_all().then(|result| {
                    result.context(ErrorKind::Initialize(
                        InitializeErrorReason::DpsProvisioningClient,
                    ))?;
                    Ok((prov_result, runtime))
                });
                Either::A(remove)
            } else {
                Either::B(future::ok((prov_result, runtime)))
            }
        })
        .and_then(move |(prov_result, runtime)| {
            let k = tpm_hsm
                .get(&KeyIdentity::Device, "primary")
                .context(ErrorKind::Initialize(
                    InitializeErrorReason::DpsProvisioningClient,
                ))?;
            let derived_key_store = DerivedKeyStore::new(k.clone());
            Ok((derived_key_store, prov_result, k, runtime))
        });

    tokio_runtime.block_on(provision)
}

fn start_runtime<K, HC>(
    runtime: &DockerModuleRuntime,
    id_man: &HubIdentityManager<DerivedKeyStore<K>, HC, K>,
    hostname: &str,
    device_id: &str,
    settings: &Settings<DockerConfig>,
    shutdown: Receiver<()>,
) -> Result<impl Future<Item = (), Error = Error>, Error>
where
    K: 'static + Sign + Clone + Send + Sync,
    HC: 'static + ClientImpl,
{
    let spec = settings.agent().clone();
    let env = build_env(spec.env(), hostname, device_id, settings);
    let mut spec = ModuleSpec::<DockerConfig>::new(
        EDGE_RUNTIME_MODULE_NAME.to_string(),
        spec.type_().to_string(),
        spec.config().clone(),
        env,
    )
    .context(ErrorKind::Initialize(InitializeErrorReason::EdgeRuntime))?;

    // volume mount management and workload URIs
    vol_mount_uri(
        spec.config_mut(),
        &[
            settings.connect().management_uri(),
            settings.connect().workload_uri(),
        ],
    )?;

    let watchdog = Watchdog::new(runtime.clone(), id_man.clone());
    let runtime_future = watchdog
        .run_until(spec, EDGE_RUNTIME_MODULEID, shutdown.map_err(|_| ()))
        .map_err(|err| Error::from(err.context(ErrorKind::Watchdog)));

    Ok(runtime_future)
}

fn vol_mount_uri(config: &mut DockerConfig, uris: &[&Url]) -> Result<(), Error> {
    let create_options = config
        .clone_create_options()
        .context(ErrorKind::Initialize(InitializeErrorReason::EdgeRuntime))?;
    let host_config = create_options
        .host_config()
        .cloned()
        .unwrap_or_else(HostConfig::new);
    let mut binds = host_config.binds().map_or_else(Vec::new, ToOwned::to_owned);

    // if the url is a domain socket URL then vol mount it into the container
    for uri in uris {
        if uri.scheme() == UNIX_SCHEME {
            let path = uri.to_uds_file_path().context(ErrorKind::Initialize(
                InitializeErrorReason::InvalidSocketUri,
            ))?;
            // On Windows we mount the parent folder because we can't mount the
            // socket files directly
            #[cfg(windows)]
            let path = path
                .parent()
                .ok_or_else(|| ErrorKind::Initialize(InitializeErrorReason::InvalidSocketUri))?;
            let path = path
                .to_str()
                .ok_or_else(|| ErrorKind::Initialize(InitializeErrorReason::InvalidSocketUri))?
                .to_string();
            let bind = format!("{}:{}", &path, &path);
            if !binds.contains(&bind) {
                binds.push(bind);
            }
        }
    }

    if !binds.is_empty() {
        let host_config = host_config.with_binds(binds);
        let create_options = create_options.with_host_config(host_config);

        config.set_create_options(create_options);
    }

    Ok(())
}

// Add the environment variables needed by the EdgeAgent.
fn build_env(
    spec_env: &HashMap<String, String>,
    hostname: &str,
    device_id: &str,
    settings: &Settings<DockerConfig>,
) -> HashMap<String, String> {
    let mut env = HashMap::new();
    env.insert(HOSTNAME_KEY.to_string(), hostname.to_string());
    env.insert(
        GATEWAY_HOSTNAME_KEY.to_string(),
        settings.hostname().to_string().to_lowercase(),
    );
    env.insert(DEVICEID_KEY.to_string(), device_id.to_string());
    env.insert(MODULEID_KEY.to_string(), EDGE_RUNTIME_MODULEID.to_string());
    env.insert(
        WORKLOAD_URI_KEY.to_string(),
        settings.connect().workload_uri().to_string(),
    );
    env.insert(
        MANAGEMENT_URI_KEY.to_string(),
        settings.connect().management_uri().to_string(),
    );
    env.insert(AUTHSCHEME_KEY.to_string(), AUTH_SCHEME.to_string());
    env.insert(
        EDGE_RUNTIME_MODE_KEY.to_string(),
        EDGE_RUNTIME_MODE.to_string(),
    );
    env.insert(
        EDGE_NETWORKID_KEY.to_string(),
        settings.moby_runtime().network().to_string(),
    );
    for (key, val) in spec_env.iter() {
        env.insert(key.clone(), val.clone());
    }
    env.insert(API_VERSION_KEY.to_string(), API_VERSION.to_string());
    env
}

fn start_management<K, HC>(
    settings: &Settings<DockerConfig>,
    mgmt: &DockerModuleRuntime,
    id_man: &HubIdentityManager<DerivedKeyStore<K>, HC, K>,
    shutdown: Receiver<()>,
) -> impl Future<Item = (), Error = Error>
where
    K: 'static + Sign + Clone + Send + Sync,
    HC: 'static + ClientImpl + Send + Sync,
{
    info!("Starting management API...");

    let label = "mgmt".to_string();
    let url = settings.listen().management_uri().clone();

    ManagementService::new(mgmt, id_man)
        .then(move |service| -> Result<_, Error> {
            let service = service.context(ErrorKind::Initialize(
                InitializeErrorReason::ManagementService,
            ))?;
            let service = LoggingService::new(label, service);
            info!("Listening on {} with 1 thread for management API.", url);
            let run = Http::new()
                .bind_url(url.clone(), service)
                .map_err(|err| {
                    err.context(ErrorKind::Initialize(
                        InitializeErrorReason::ManagementService,
                    ))
                })?
                .run_until(shutdown.map_err(|_| ()))
                .map_err(|err| Error::from(err.context(ErrorKind::ManagementService)));
            Ok(run)
        })
        .flatten()
}

fn start_workload<K, C, W>(
    settings: &Settings<DockerConfig>,
    key_store: &K,
    runtime: &DockerModuleRuntime,
    shutdown: Receiver<()>,
    crypto: &C,
    config: W,
) -> impl Future<Item = (), Error = Error>
where
    K: KeyStore + Clone + Send + Sync + 'static,
    C: CreateCertificate
        + Decrypt
        + Encrypt
        + GetTrustBundle
        + MasterEncryptionKey
        + Clone
        + Send
        + Sync
        + 'static,
    W: WorkloadConfig + Clone + Send + Sync + 'static,
{
    info!("Starting workload API...");

    let label = "work".to_string();
    let url = settings.listen().workload_uri().clone();

    WorkloadService::new(key_store, crypto.clone(), runtime, config)
        .then(move |service| -> Result<_, Error> {
            let service = service.context(ErrorKind::Initialize(
                InitializeErrorReason::WorkloadService,
            ))?;
            let service = LoggingService::new(label, service);
            let run = Http::new()
                .bind_url(url.clone(), service)
                .map_err(|err| {
                    err.context(ErrorKind::Initialize(
                        InitializeErrorReason::WorkloadService,
                    ))
                })?
                .run_until(shutdown.map_err(|_| ()))
                .map_err(|err| Error::from(err.context(ErrorKind::WorkloadService)));
            info!("Listening on {} with 1 thread for workload API.", url);
            Ok(run)
        })
        .flatten()
}

#[cfg(test)]
mod tests {
    use std::fmt;
    use std::io::Read;

    use tempdir::TempDir;

    use edgelet_core::ModuleRuntimeState;
    use edgelet_core::{KeyBytes, PrivateKey};
    use edgelet_test_utils::cert::TestCert;
    use edgelet_test_utils::module::*;

    use super::*;

    #[cfg(unix)]
    static SETTINGS: &str = "test/linux/sample_settings.yaml";
    #[cfg(unix)]
    static SETTINGS1: &str = "test/linux/sample_settings1.yaml";

    #[cfg(windows)]
    static SETTINGS: &str = "test/windows/sample_settings.yaml";
    #[cfg(windows)]
    static SETTINGS1: &str = "test/windows/sample_settings1.yaml";

    #[derive(Clone, Copy, Debug, Fail)]
    pub struct Error;

    impl fmt::Display for Error {
        fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
            write!(f, "Error")
        }
    }

    // impl From<Error> for super::Error {
    //     fn from(_error: Error) -> Self {
    //         super::Error::from(ErrorKind::Var)
    //     }
    // }

    struct TestCrypto {}

    impl MasterEncryptionKey for TestCrypto {
        fn create_key(&self) -> Result<(), edgelet_core::Error> {
            Ok(())
        }
        fn destroy_key(&self) -> Result<(), edgelet_core::Error> {
            Ok(())
        }
    }

    impl CreateCertificate for TestCrypto {
        type Certificate = TestCert;

        fn create_certificate(
            &self,
            _properties: &CertificateProperties,
        ) -> Result<Self::Certificate, edgelet_core::Error> {
            Ok(TestCert::default()
                .with_cert(vec![1, 2, 3])
                .with_private_key(PrivateKey::Key(KeyBytes::Pem("some key".to_string())))
                .with_fail_pem(false)
                .with_fail_private_key(false))
        }

        fn destroy_certificate(&self, _alias: String) -> Result<(), edgelet_core::Error> {
            Ok(())
        }
    }

    #[test]
    fn default_settings_raise_unconfigured_error() {
        let settings = Settings::<DockerConfig>::new(None).unwrap();
        let main = Main::new(settings);
        let shutdown_signal = signal::shutdown();
        let result = main.run_until(shutdown_signal);
        match result.unwrap_err().kind() {
            ErrorKind::Initialize(InitializeErrorReason::NotConfigured) => (),
            kind => panic!("Expected `NotConfigured` but got {:?}", kind),
        }
    }

    #[test]
    fn settings_first_time_creates_backup() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::<DockerConfig>::new(Some(SETTINGS)).unwrap();
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {};
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        check_settings_state(
            tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
        )
        .unwrap();
        let expected = serde_json::to_string(&settings).unwrap();
        let expected_sha = Sha256::digest_str(&expected);
        let expected_base64 = base64::encode(&expected_sha);
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();

        assert_eq!(expected_base64, written);
    }

    #[test]
    fn settings_change_creates_new_backup() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::<DockerConfig>::new(Some(SETTINGS)).unwrap();
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {};
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        check_settings_state(
            tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
        )
        .unwrap();
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();

        let settings1 = Settings::<DockerConfig>::new(Some(SETTINGS1)).unwrap();
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        check_settings_state(
            tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings1,
            &runtime,
            &crypto,
            &mut tokio_runtime,
        )
        .unwrap();
        let expected = serde_json::to_string(&settings1).unwrap();
        let expected_sha = Sha256::digest_str(&expected);
        let expected_base64 = base64::encode(&expected_sha);
        let mut written1 = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written1)
            .unwrap();

        assert_eq!(expected_base64, written1);
        assert_ne!(written1, written);
    }

    #[test]
    fn get_proxy_uri_recognizes_https_proxy() {
        // Use existing "https_proxy" env var if it's set, otherwise invent one
        let proxy_val = env::var("https_proxy")
            .unwrap_or_else(|_| "https://example.com".to_string())
            .parse::<Uri>()
            .unwrap()
            .to_string();

        assert_eq!(
            get_proxy_uri(Some(proxy_val.clone()))
                .unwrap()
                .unwrap()
                .to_string(),
            proxy_val
        );
    }

    #[test]
    fn get_proxy_uri_allows_credentials_in_authority() {
        let proxy_val = "https://username:password@example.com/".to_string();
        assert_eq!(
            get_proxy_uri(Some(proxy_val.clone()))
                .unwrap()
                .unwrap()
                .to_string(),
            proxy_val
        );

        let proxy_val = "https://username%2f:password%2f@example.com/".to_string();
        assert_eq!(
            get_proxy_uri(Some(proxy_val.clone()))
                .unwrap()
                .unwrap()
                .to_string(),
            proxy_val
        );
    }
}
