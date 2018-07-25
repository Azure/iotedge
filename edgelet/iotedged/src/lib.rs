// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

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
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hsm;
extern crate hyper;
extern crate hyper_tls;
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
extern crate tokio_core;
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

use docker::models::HostConfig;
use edgelet_core::{
    CertificateProperties, CertificateType, CertificateIssuer
};
use edgelet_core::crypto::{
    CreateCertificate, Decrypt, DerivedKeyStore, Encrypt, GetTrustBundle, KeyIdentity, KeyStore,
    MasterEncryptionKey, MemoryKey, MemoryKeyStore, Sign, IOTEDGED_CA_ALIAS
};
use edgelet_core::watchdog::Watchdog;
use edgelet_core::{ModuleRuntime, ModuleSpec};
use edgelet_docker::{DockerConfig, DockerModuleRuntime};
use edgelet_hsm::tpm::{TpmKey, TpmKeyStore};
use edgelet_hsm::Crypto;
use edgelet_http::client::Client as HttpClient;
use edgelet_http::logging::LoggingService;
use edgelet_http::{ApiVersionService, HyperExt, API_VERSION};
use edgelet_http_mgmt::ManagementService;
use edgelet_http_workload::WorkloadService;
use edgelet_iothub::{HubIdentityManager, SasTokenSource};
use futures::future;
use futures::future::Either;
use futures::sync::oneshot::{self, Receiver};
use futures::Future;
use hsm::tpm::Tpm;
use hsm::ManageTpmKeys;
use hyper::client::Service;
use hyper::server::Http;
use hyper::{Client as HyperClient, Error as HyperError, Request, Response};
use hyper_tls::HttpsConnector;
use iothubservice::DeviceClient;
use provisioning::provisioning::{
    BackupProvisioning, DpsProvisioning, ManualProvisioning, Provision, ProvisioningResult,
};
use sha2::{Digest, Sha256};
use tokio_core::reactor::{Core, Handle};
use url::Url;

use settings::{Dps, Manual, Provisioning, Settings};

pub use self::error::{Error, ErrorKind};

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
const DNS_WORKER_THREADS: usize = 4;
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

pub struct Main {
    settings: Settings<DockerConfig>,
    reactor: Core,
}

impl Main {
    pub fn new(settings: Settings<DockerConfig>) -> Result<Self, Error> {
        let reactor = Core::new()?;
        let main = Main { settings, reactor };
        Ok(main)
    }

    pub fn handle(&self) -> Handle {
        self.reactor.handle()
    }

    pub fn run_until<F>(self, shutdown_signal: F) -> Result<(), Error>
    where
        F: Future<Item = (), Error = ()> + 'static,
    {
        let Main {
            settings,
            reactor: mut core,
        } = self;

        let handle: Handle = core.handle().clone();
        let hyper_client = HyperClient::configure()
            .connector(HttpsConnector::new(DNS_WORKER_THREADS, &handle)?)
            .build(&handle);

        info!(
            "Using runtime network id {}",
            settings.moby_runtime().network()
        );
        let runtime = DockerModuleRuntime::new(settings.moby_runtime().uri(), &handle)?
            .with_network_id(settings.moby_runtime().network().to_string());

        init_docker_runtime(&runtime, &mut core)?;

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
        let crypto = Crypto::new()?;
        info!("Finished initializing hsm.");

        // Detect if the settings were changed and if the device needs to be reconfigured
        let cache_subdir_path = Path::new(&settings.homedir()).join(EDGE_SETTINGS_SUBDIR);
        check_settings_state(
            cache_subdir_path.clone(),
            EDGE_SETTINGS_STATE_FILENAME,
            &settings,
            &runtime,
            &mut core,
            &crypto,
        )?;

        info!("Provisioning edge device...");
        match settings.provisioning() {
            Provisioning::Manual(manual) => {
                let (key_store, provisioning_result, root_key) =
                    manual_provision(&manual, &mut core)?;
                info!("Finished provisioning edge device.");
                start_api(
                    &settings,
                    core,
                    hyper_client,
                    &runtime,
                    &key_store,
                    &provisioning_result,
                    root_key,
                    shutdown_signal,
                    &crypto,
                )?;
            }
            Provisioning::Dps(dps) => {
                let dps_path = cache_subdir_path.join(EDGE_PROVISIONING_BACKUP_FILENAME);
                let (key_store, provisioning_result, root_key) =
                    dps_provision(&dps, hyper_client.clone(), &mut core, dps_path, &runtime)?;
                info!("Finished provisioning edge device.");
                start_api(
                    &settings,
                    core,
                    hyper_client,
                    &runtime,
                    &key_store,
                    &provisioning_result,
                    root_key,
                    shutdown_signal,
                    &crypto,
                )?;
            }
        };

        info!("Shutdown complete.");
        Ok(())
    }
}

fn prepare_workload_ca<C>(crypto: &C) -> Result<(), Error>
where
    C: CreateCertificate
{
    let edgelet_ca_props = CertificateProperties::new(
        IOTEDGED_VALIDITY,
        IOTEDGED_COMMONNAME.to_string(),
        CertificateType::Ca,
        IOTEDGED_CA_ALIAS.to_string(),
    ).with_issuer(CertificateIssuer::DeviceCa);

    let _workload_ca_cert = crypto.create_certificate(&edgelet_ca_props)
        .map_err(Error::from)?;
    Ok(())
}

fn destroy_workload_ca<C>(crypto: &C) -> Result<(), Error>
where
    C: CreateCertificate
{
    crypto.destroy_certificate(IOTEDGED_CA_ALIAS.to_string())
        .map_err(Error::from)?;
    Ok(())
}

fn check_settings_state<M, C>(
    subdir_path: PathBuf,
    filename: &str,
    settings: &Settings<DockerConfig>,
    runtime: &M,
    core: &mut Core,
    crypto: &C,
) -> Result<(), Error>
where
    M: ModuleRuntime,
    M::Error: Into<Error>,
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
        match prepare_workload_ca(crypto) {
            Ok(()) => info!("Obtaining workload CA succeeded."),
            Err(_) => {
                reconfig_reqd = true;
                info!("Obtaining workload CA failed. Triggering reconfiguration");
            }
        };
    }
    if reconfig_reqd {
        reconfigure(subdir_path, filename, settings, runtime, crypto, core)?;
    }
    Ok(())
}

fn reconfigure<M, C>(
    subdir: PathBuf,
    filename: &str,
    settings: &Settings<DockerConfig>,
    runtime: &M,
    crypto: &C,
    core: &mut Core,
) -> Result<(), Error>
where
    M: ModuleRuntime,
    M::Error: Into<Error>,
    C: MasterEncryptionKey  + CreateCertificate,
{
    // Remove all edge containers and destroy the cache (settings and dps backup)
    info!("Removing all modules...");
    core.run(runtime.remove_all().map_err(|err| err.into()))?;
    info!("Finished removing modules.");

    // Ignore errors from this operation because we could be recovering from a previous bad
    // configuration and shouldn't stall the current configuration because of that
    let _u = fs::remove_dir_all(subdir.clone());

    let path = subdir.join(filename);

    DirBuilder::new().recursive(true).create(subdir)?;

    // Generate a new master encryption key and save the new settings
    crypto.create_key()?;
    // regenerate the workload CA certificate
    destroy_workload_ca(crypto)?;
    prepare_workload_ca(crypto)?;
    let mut file = File::create(path)?;
    serde_json::to_string(settings)
        .map_err(Error::from)
        .map(|s| Sha256::digest_str(&s))
        .map(|s| base64::encode(&s))
        .and_then(|sb| file.write_all(sb.as_bytes()).map_err(Error::from))
}

#[cfg_attr(feature = "cargo-clippy", allow(too_many_arguments))]
fn start_api<S, K, F, C>(
    settings: &Settings<DockerConfig>,
    mut core: Core,
    hyper_client: S,
    runtime: &DockerModuleRuntime,
    key_store: &DerivedKeyStore<K>,
    provisioning_result: &ProvisioningResult,
    root_key: K,
    shutdown_signal: F,
    crypto: &C,
) -> Result<(), Error>
where
    F: Future<Item = (), Error = ()> + 'static,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    K: 'static + Sign + Clone,
    C: 'static
        + CreateCertificate
        + Decrypt
        + Encrypt
        + GetTrustBundle
        + MasterEncryptionKey
        + Clone,
{
    let hub_name = provisioning_result.hub_name();
    let device_id = provisioning_result.device_id();
    let hostname = format!("https://{}", hub_name);
    let token_source = SasTokenSource::new(hub_name.to_string(), device_id.to_string(), root_key);
    let http_client = HttpClient::new(
        hyper_client,
        Some(token_source),
        IOTHUB_API_VERSION,
        Url::parse(&hostname)?,
    )?;
    let device_client = DeviceClient::new(http_client, &device_id)?;
    let id_man = HubIdentityManager::new(key_store.clone(), device_client);

    let (mgmt_tx, mgmt_rx) = oneshot::channel();
    let (work_tx, work_rx) = oneshot::channel();

    let mgmt = start_management(&settings, &core.handle(), &runtime, &id_man, mgmt_rx)?;

    let workload = start_workload(
        &settings,
        key_store,
        &core.handle(),
        &runtime,
        work_rx,
        crypto,
    )?;

    let (runt_tx, runt_rx) = oneshot::channel();
    let edge_rt = start_runtime(&runtime, &id_man, &hub_name, &device_id, &settings, runt_rx)?;

    // Wait for the watchdog to finish, and then send signal to the workload and management services.
    // This way the edgeAgent can finish shutting down all modules.
    let edge_rt_with_cleanup = edge_rt.and_then(|_| {
        mgmt_tx.send(()).unwrap_or(());
        work_tx.send(()).unwrap_or(());
        future::ok(())
    });

    let shutdown = shutdown_signal.map(move |_| {
        debug!("shutdown signaled");
        // Signal the watchdog to shutdown
        runt_tx.send(()).unwrap_or(());
    });

    core.handle().spawn(shutdown);

    core.run(mgmt.join3(workload, edge_rt_with_cleanup))?;

    Ok(())
}

fn init_docker_runtime(runtime: &DockerModuleRuntime, core: &mut Core) -> Result<(), Error> {
    info!("Initializing the module runtime...");
    core.run(runtime.init())?;
    info!("Finished initializing the module runtime.");
    Ok(())
}

fn manual_provision(
    provisioning: &Manual,
    core: &mut Core,
) -> Result<(DerivedKeyStore<MemoryKey>, ProvisioningResult, MemoryKey), Error> {
    let manual = ManualProvisioning::new(provisioning.device_connection_string())?;
    let memory_hsm = MemoryKeyStore::new();
    let provision = manual
        .provision(memory_hsm.clone())
        .map_err(Error::from)
        .and_then(move |prov_result| {
            memory_hsm
                .get(&KeyIdentity::Device, "primary")
                .map_err(Error::from)
                .and_then(|k| {
                    let derived_key_store = DerivedKeyStore::new(k.clone());
                    Ok((derived_key_store, prov_result, k))
                })
        });
    core.run(provision)
}

fn dps_provision<S, M>(
    provisioning: &Dps,
    hyper_client: S,
    core: &mut Core,
    backup_path: PathBuf,
    runtime: &M,
) -> Result<(DerivedKeyStore<TpmKey>, ProvisioningResult, TpmKey), Error>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    M: ModuleRuntime,
    M::Error: Into<Error>,
{
    let tpm = Tpm::new().map_err(Error::from)?;
    let ek_result = tpm.get_ek().map_err(Error::from)?;
    let srk_result = tpm.get_srk().map_err(Error::from)?;
    let dps = DpsProvisioning::new(
        hyper_client,
        provisioning.global_endpoint().clone(),
        provisioning.scope_id().to_string(),
        provisioning.registration_id().to_string(),
        "2017-11-15",
        ek_result,
        srk_result,
    )?;
    let tpm_hsm = TpmKeyStore::from_hsm(tpm)?;
    let provision_with_file_backup = BackupProvisioning::new(dps, backup_path);
    let provision = provision_with_file_backup
        .provision(tpm_hsm.clone())
        .map_err(Error::from)
        .and_then(|prov_result| {
            if prov_result.reconfigure() {
                info!("Successful DPS provisioning. This will trigger reconfiguration of modules.");
                // Each time DPS provisions, it gets back a new device key. This results in obsolete
                // module keys in IoTHub from the previous provisioning. We delete all containers
                // after each DPS provisioning run so that IoTHub can be updated with new module
                // keys when the deployment is executed by EdgeAgent.
                let remove = runtime
                    .remove_all()
                    .map_err(|err| err.into())
                    .map(|_| prov_result);
                Either::A(remove)
            } else {
                Either::B(future::ok(prov_result))
            }
        })
        .and_then(move |prov_result| {
            tpm_hsm
                .get(&KeyIdentity::Device, "primary")
                .map_err(Error::from)
                .and_then(|k| {
                    let derived_key_store = DerivedKeyStore::new(k.clone());
                    Ok((derived_key_store, prov_result, k))
                })
        });

    core.run(provision)
}

fn start_runtime<K, S>(
    runtime: &DockerModuleRuntime,
    id_man: &HubIdentityManager<DerivedKeyStore<K>, S, K>,
    hostname: &str,
    device_id: &str,
    settings: &Settings<DockerConfig>,
    shutdown: Receiver<()>,
) -> Result<impl Future<Item = (), Error = Error>, Error>
where
    K: 'static + Sign + Clone,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    let spec = settings.agent().clone();
    let env = build_env(spec.env(), hostname, device_id, settings);
    let mut spec = ModuleSpec::<DockerConfig>::new(
        EDGE_RUNTIME_MODULE_NAME,
        spec.type_(),
        spec.config().clone(),
        env,
    )?;

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
        .map_err(Error::from);

    Ok(runtime_future)
}

fn vol_mount_uri(config: &mut DockerConfig, uris: &[&Url]) -> Result<(), Error> {
    let create_options = config.clone_create_options()?;
    let host_config = create_options
        .host_config()
        .cloned()
        .unwrap_or_else(HostConfig::new);
    let mut binds = host_config.binds().cloned().unwrap_or_else(Vec::new);

    // if the url is a domain socket URL then vol mount it into the container
    for uri in uris {
        if uri.scheme() == UNIX_SCHEME {
            binds.push(format!("{}:{}", uri.path(), uri.path()));
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

fn start_management<K, S>(
    settings: &Settings<DockerConfig>,
    handle: &Handle,
    mgmt: &DockerModuleRuntime,
    id_man: &HubIdentityManager<DerivedKeyStore<K>, S, K>,
    shutdown: Receiver<()>,
) -> Result<impl Future<Item = (), Error = Error>, Error>
where
    K: 'static + Sign + Clone,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    info!("Starting management API...");

    let label = "mgmt".to_string();
    let url = settings.listen().management_uri().clone();
    let server_handle = handle.clone();
    let service = LoggingService::new(
        label,
        ApiVersionService::new(ManagementService::new(mgmt, id_man)?),
    );

    let run = Http::new()
        .bind_handle(url.clone(), server_handle, service)?
        .run_until(shutdown.map_err(|_| ()))
        .map_err(Error::from);
    info!("Listening on {} with 1 thread for management API.", url);
    Ok(run)
}

fn start_workload<K, C>(
    settings: &Settings<DockerConfig>,
    key_store: &K,
    handle: &Handle,
    runtime: &DockerModuleRuntime,
    shutdown: Receiver<()>,
    crypto: &C,
) -> Result<impl Future<Item = (), Error = Error>, Error>
where
    K: 'static + KeyStore + Clone,
    C: 'static
        + CreateCertificate
        + Decrypt
        + Encrypt
        + GetTrustBundle
        + MasterEncryptionKey
        + Clone,
{
    info!("Starting workload API...");

    let label = "work".to_string();
    let url = settings.listen().workload_uri().clone();
    let server_handle = handle.clone();
    let service = LoggingService::new(
        label,
        ApiVersionService::new(WorkloadService::new(key_store, crypto.clone(), runtime)?),
    );

    let run = Http::new()
        .bind_handle(url.clone(), server_handle, service)?
        .run_until(shutdown.map_err(|_| ()))
        .map_err(Error::from);
    info!("Listening on {} with 1 thread for workload API.", url);
    Ok(run)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Read;

    use edgelet_core::ModuleRuntimeState;
    use edgelet_test_utils::module::*;
    use tempdir::TempDir;
    use edgelet_test_utils::cert::TestCert;
    use edgelet_core::{PrivateKey, KeyBytes};

    #[cfg(unix)]
    static SETTINGS: &str = "test/linux/sample_settings.yaml";
    #[cfg(unix)]
    static SETTINGS1: &str = "test/linux/sample_settings1.yaml";

    #[cfg(windows)]
    static SETTINGS: &str = "test/windows/sample_settings.yaml";
    #[cfg(windows)]
    static SETTINGS1: &str = "test/windows/sample_settings1.yaml";

    #[derive(Clone, Debug, Fail)]
    pub enum Error {
        #[fail(display = "General error")]
        General,
    }

    impl From<Error> for super::Error {
        fn from(_error: Error) -> super::Error {
            super::Error::from(ErrorKind::Var)
        }
    }

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
        ) -> Result<TestCert, edgelet_core::Error> {
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
    fn settings_first_time_creates_backup() {
        let mut core = Core::new().unwrap();
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::<DockerConfig>::new(Some(SETTINGS)).unwrap();
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {};
        assert_eq!(
            check_settings_state(
                tmp_dir.path().to_path_buf(),
                "settings_state",
                &settings,
                &runtime,
                &mut core,
                &crypto
            ).unwrap(),
            ()
        );
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
        let mut core = Core::new().unwrap();
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::<DockerConfig>::new(Some(SETTINGS)).unwrap();
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {};
        assert_eq!(
            check_settings_state(
                tmp_dir.path().to_path_buf(),
                "settings_state",
                &settings,
                &runtime,
                &mut core,
                &crypto
            ).unwrap(),
            ()
        );
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();

        let settings1 = Settings::<DockerConfig>::new(Some(SETTINGS1)).unwrap();
        assert_eq!(
            check_settings_state(
                tmp_dir.path().to_path_buf(),
                "settings_state",
                &settings1,
                &runtime,
                &mut core,
                &crypto
            ).unwrap(),
            ()
        );
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
}
