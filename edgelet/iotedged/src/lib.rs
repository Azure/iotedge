// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::doc_markdown, // clippy want the "IoT" of "IoT Hub" in a code fence
    clippy::module_name_repetitions,
    clippy::shadow_unrelated,
    clippy::use_self,
)]

pub mod app;
mod error;
pub mod logging;
pub mod signal;
pub mod workload;

#[cfg(not(target_os = "windows"))]
pub mod unix;

#[cfg(target_os = "windows")]
pub mod windows;

use std::collections::HashMap;
use std::env;
use std::fs;
use std::fs::{DirBuilder, File, OpenOptions};
use std::io::{Read, Write};
use std::path::{Path, PathBuf};
use std::sync::Arc;

use failure::{Context, Fail, ResultExt};
use futures::future::{Either, IntoFuture};
use futures::sync::oneshot::{self, Receiver};
use futures::{future, Future};
use hyper::server::conn::Http;
use hyper::{Body, Request, Uri};
use log::{debug, info, Level};
use serde::de::DeserializeOwned;
use serde::Serialize;
use sha2::{Digest, Sha256};
use url::Url;

use dps::DPS_API_VERSION;
use edgelet_core::crypto::{
    Activate, CreateCertificate, Decrypt, DerivedKeyStore, Encrypt, GetIssuerAlias, GetTrustBundle,
    KeyIdentity, KeyStore, MasterEncryptionKey, MemoryKey, MemoryKeyStore, Sign, IOTEDGED_CA_ALIAS,
};
use edgelet_core::watchdog::Watchdog;
use edgelet_core::{
    AttestationMethod, Authenticator, Certificate, CertificateIssuer, CertificateProperties,
    CertificateType, Dps, MakeModuleRuntime, Manual, Module, ModuleRuntime,
    ModuleRuntimeErrorReason, ModuleSpec, Provisioning,
    ProvisioningResult as CoreProvisioningResult, RuntimeSettings, SymmetricKeyAttestationInfo,
    TpmAttestationInfo, WorkloadConfig, DEFAULT_CONNECTION_STRING,
};
use edgelet_hsm::tpm::{TpmKey, TpmKeyStore};
use edgelet_hsm::{Crypto, HsmLock};
use edgelet_http::certificate_manager::CertificateManager;
use edgelet_http::client::{Client as HttpClient, ClientImpl};
use edgelet_http::logging::LoggingService;
use edgelet_http::{HyperExt, MaybeProxyClient, API_VERSION};
use edgelet_http_external_provisioning::ExternalProvisioningClient;
use edgelet_http_mgmt::ManagementService;
use edgelet_http_workload::WorkloadService;
use edgelet_iothub::{HubIdentityManager, SasTokenSource};
use edgelet_utils::log_failure;
pub use error::{Error, ErrorKind, InitializeErrorReason};
use hsm::tpm::Tpm;
use hsm::ManageTpmKeys;
use iothubservice::DeviceClient;
use provisioning::provisioning::{
    AuthType, BackupProvisioning, DpsSymmetricKeyProvisioning, DpsTpmProvisioning,
    ExternalProvisioning, ManualProvisioning, Provision, ProvisioningResult, ReprovisioningStatus,
};

use crate::error::ExternalProvisioningErrorReason;
use crate::workload::WorkloadData;

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

/// The HSM lib expects this variable to be set to the endpoint of the external provisioning environment in the 'external'
/// provisioning mode.
const EXTERNAL_PROVISIONING_ENDPOINT_KEY: &str = "IOTEDGE_EXTERNAL_PROVISIONING_ENDPOINT";

/// This is the key for the largest API version that this edgelet supports
const API_VERSION_KEY: &str = "IOTEDGE_APIVERSION";

const IOTHUB_API_VERSION: &str = "2017-11-08-preview";

/// This is the name of the provisioning backup file
const EDGE_PROVISIONING_BACKUP_FILENAME: &str = "provisioning_backup.json";

/// This is the name of the settings backup file
const EDGE_SETTINGS_STATE_FILENAME: &str = "settings_state";

/// This is the name of the cache subdirectory for settings state
const EDGE_SETTINGS_SUBDIR: &str = "cache";

/// These are the properties of the workload CA certificate
const IOTEDGED_VALIDITY: u64 = 7_776_000;
// 90 days
const IOTEDGED_COMMONNAME: &str = "iotedged workload ca";
const IOTEDGED_TLS_COMMONNAME: &str = "iotedged";
const IOTEDGED_MIN_EXPIRATION_DURATION: i64 = 300;
// 5 mins
const IOTEDGE_ID_CERT_MAX_DURATION_SECS: i64 = 7200;
// 2 hours
const IOTEDGE_SERVER_CERT_MAX_DURATION_SECS: i64 = 7_776_000; // 90 days

#[derive(PartialEq)]
enum StartApiReturnStatus {
    Restart,
    Shutdown,
}

pub struct Main<M>
where
    M: MakeModuleRuntime,
{
    settings: M::Settings,
}

impl<M> Main<M>
where
    M: MakeModuleRuntime<ProvisioningResult = ProvisioningResult> + Send + 'static,
    M::ModuleRuntime: 'static + Authenticator<Request = Request<Body>> + Clone + Send + Sync,
    <<M::ModuleRuntime as ModuleRuntime>::Module as Module>::Config:
        Clone + DeserializeOwned + Serialize,
    M::Settings: 'static + Clone + Serialize,
    <M::ModuleRuntime as ModuleRuntime>::Logs: Into<Body>,
    <M::ModuleRuntime as Authenticator>::Error: Fail + Sync,
    for<'r> &'r <M::ModuleRuntime as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
{
    pub fn new(settings: M::Settings) -> Self {
        Main { settings }
    }

    // Allowing cognitive complexity errors for now. TODO: Refactor method later.
    #[allow(clippy::cognitive_complexity)]
    pub fn run_until<F, G>(self, make_shutdown_signal: G) -> Result<(), Error>
    where
        F: Future<Item = (), Error = ()> + Send + 'static,
        G: Fn() -> F,
    {
        let Main { settings } = self;
        let hsm_lock = HsmLock::new();

        let mut tokio_runtime = tokio::runtime::Runtime::new()
            .context(ErrorKind::Initialize(InitializeErrorReason::Tokio))?;

        if let Provisioning::Manual(ref manual) = settings.provisioning() {
            if manual.device_connection_string() == DEFAULT_CONNECTION_STRING {
                return Err(Error::from(ErrorKind::Initialize(
                    InitializeErrorReason::NotConfigured,
                )));
            }
        }

        if let Provisioning::External(ref external) = settings.provisioning() {
            // Set the external provisioning endpoint environment variable for use by the custom HSM library.
            env::set_var(
                EXTERNAL_PROVISIONING_ENDPOINT_KEY,
                external.endpoint().as_str(),
            );
        }

        let hyper_client = MaybeProxyClient::new(get_proxy_uri(None)?, None, None)
            .context(ErrorKind::Initialize(InitializeErrorReason::HttpClient))?;

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
        let crypto = Crypto::new(hsm_lock.clone())
            .context(ErrorKind::Initialize(InitializeErrorReason::Hsm))?;
        info!("Finished initializing hsm.");

        let cache_subdir_path = Path::new(&settings.homedir()).join(EDGE_SETTINGS_SUBDIR);
        // make sure the cache directory exists
        DirBuilder::new()
            .recursive(true)
            .create(&cache_subdir_path)
            .context(ErrorKind::Initialize(
                InitializeErrorReason::CreateCacheDirectory,
            ))?;

        macro_rules! start_edgelet {
            ($key_store:ident, $provisioning_result:ident, $root_key:ident) => {{
                info!("Finished provisioning edge device.");

                let runtime = init_runtime::<M>(
                    settings.clone(),
                    &mut tokio_runtime,
                    $provisioning_result.clone(),
                )?;

                if $provisioning_result.reconfigure() != ReprovisioningStatus::DeviceDataNotUpdated {
                    // If this device was reprovisioned and the device key was updated it causes
                    // module keys to be obsoleted in IoTHub from the previous provisioning. We therefore
                    // delete all containers after each DPS provisioning run so that IoTHub can be updated
                    // with new module keys when the deployment is executed by EdgeAgent.
                    info!(
                        "Reprovisioning status {:?} will trigger reconfiguration of modules.",
                        $provisioning_result.reconfigure()
                    );

                    tokio_runtime
                        .block_on(runtime.remove_all())
                        .context(ErrorKind::Initialize(
                            InitializeErrorReason::RemoveExistingModules,
                        ))?;
                }

                // Detect if the settings were changed and if the device needs to be reconfigured
                check_settings_state::<M, _>(
                    cache_subdir_path.clone(),
                    EDGE_SETTINGS_STATE_FILENAME,
                    &settings,
                    &runtime,
                    &crypto,
                    &mut tokio_runtime,
                )?;

                let cfg = WorkloadData::new(
                    $provisioning_result.hub_name().to_string(),
                    $provisioning_result.device_id().to_string(),
                    IOTEDGE_ID_CERT_MAX_DURATION_SECS,
                    IOTEDGE_SERVER_CERT_MAX_DURATION_SECS,
                );
                // This "do-while" loop runs until a StartApiReturnStatus::Shutdown
                // is received. If the TLS cert needs a restart, we will loop again.
                loop {
                    let code = start_api::<_, _, _, _, _, M>(
                        &settings,
                        hyper_client.clone(),
                        &runtime,
                        &$key_store,
                        cfg.clone(),
                        $root_key.clone(),
                        make_shutdown_signal(),
                        &crypto,
                        &mut tokio_runtime,
                    )?;

                    if code != StartApiReturnStatus::Restart {
                        break;
                    }
                }
            }};
        }

        info!("Provisioning edge device...");
        match settings.provisioning() {
            Provisioning::Manual(manual) => {
                info!("Starting provisioning edge device via manual mode...");
                let (key_store, provisioning_result, root_key) =
                    manual_provision(&manual, &mut tokio_runtime)?;
                start_edgelet!(key_store, provisioning_result, root_key);
            }
            Provisioning::External(external) => {
                info!("Starting provisioning edge device via external provisioning mode...");
                let external_provisioning_client =
                    ExternalProvisioningClient::new(external.endpoint()).context(
                        ErrorKind::Initialize(InitializeErrorReason::ExternalProvisioningClient(
                            ExternalProvisioningErrorReason::ClientInitialization,
                        )),
                    )?;
                let external_provisioning = ExternalProvisioning::new(external_provisioning_client);

                let provision_fut = external_provisioning
                    .provision(MemoryKeyStore::new())
                    .map_err(|err| {
                        Error::from(err.context(ErrorKind::Initialize(
                            InitializeErrorReason::ExternalProvisioningClient(
                                ExternalProvisioningErrorReason::Provisioning,
                            ),
                        )))
                    });

                let prov_result = tokio_runtime.block_on(provision_fut)?;

                let credentials = if let Some(credentials) = prov_result.credentials() {
                    credentials
                } else {
                    info!("Credentials are expected to be populated for external provisioning.");

                    return Err(Error::from(ErrorKind::Initialize(
                        InitializeErrorReason::ExternalProvisioningClient(
                            ExternalProvisioningErrorReason::InvalidCredentials,
                        ),
                    )));
                };

                match credentials.auth_type() {
                    AuthType::SymmetricKey(symmetric_key) => {
                        if let Some(key) = symmetric_key.key() {
                            let (derived_key_store, memory_key) = external_provision_payload(key);
                            start_edgelet!(derived_key_store, prov_result, memory_key);
                        } else {
                            let (derived_key_store, tpm_key) =
                                external_provision_tpm(hsm_lock.clone())?;
                            start_edgelet!(derived_key_store, prov_result, tpm_key);
                        }
                    }
                    AuthType::X509(_) => {
                        info!("Unexpected auth type. Only symmetric keys are expected");
                        return Err(Error::from(ErrorKind::Initialize(
                            InitializeErrorReason::ExternalProvisioningClient(
                                ExternalProvisioningErrorReason::InvalidAuthenticationType,
                            ),
                        )));
                    }
                };
            }
            Provisioning::Dps(dps) => {
                let dps_path = cache_subdir_path.join(EDGE_PROVISIONING_BACKUP_FILENAME);

                match dps.attestation() {
                    AttestationMethod::Tpm(ref tpm) => {
                        info!("Starting provisioning edge device via TPM...");
                        let (key_store, provisioning_result, root_key) = dps_tpm_provision(
                            &dps,
                            hyper_client.clone(),
                            dps_path,
                            &mut tokio_runtime,
                            tpm,
                            hsm_lock.clone(),
                        )?;
                        start_edgelet!(key_store, provisioning_result, root_key);
                    }
                    AttestationMethod::SymmetricKey(ref symmetric_key_info) => {
                        info!("Starting provisioning edge device via symmetric key...");
                        let (key_store, provisioning_result, root_key) =
                            dps_symmetric_key_provision(
                                &dps,
                                hyper_client.clone(),
                                dps_path,
                                &mut tokio_runtime,
                                symmetric_key_info,
                            )?;
                        start_edgelet!(key_store, provisioning_result, root_key);
                    }
                    AttestationMethod::X509(ref _x509) => {
                        panic!("Provisioning of Edge device via x509 is currently unsupported");
                        // TODO: implement
                    }
                }
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
    C: CreateCertificate + GetIssuerAlias,
{
    let issuer_alias = crypto
        .get_issuer_alias(CertificateIssuer::DeviceCa)
        .context(ErrorKind::Initialize(
            InitializeErrorReason::PrepareWorkloadCa,
        ))?;

    let issuer_ca = crypto
        .get_certificate(issuer_alias)
        .context(ErrorKind::Initialize(
            InitializeErrorReason::PrepareWorkloadCa,
        ))?;

    let issuer_validity = issuer_ca.get_valid_to().context(ErrorKind::Initialize(
        InitializeErrorReason::PrepareWorkloadCa,
    ))?;

    info!("Edge issuer CA expiration date: {:?}", issuer_validity);

    let now = chrono::Utc::now();

    let diff = issuer_validity.timestamp() - now.timestamp();

    if diff > IOTEDGED_MIN_EXPIRATION_DURATION {
        #[allow(clippy::cast_sign_loss)]
        let edgelet_ca_props = CertificateProperties::new(
            diff as u64,
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
    } else {
        Err(Error::from(ErrorKind::Initialize(
            InitializeErrorReason::IssuerCAExpiration,
        )))
    }
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

fn diff_with_cached<S>(settings: &S, path: &Path) -> bool
where
    S: Serialize,
{
    fn diff_with_cached_inner<S>(cached_settings: &S, path: &Path) -> Result<bool, DiffError>
    where
        S: Serialize,
    {
        let mut file = OpenOptions::new().read(true).open(path)?;
        let mut buffer = String::new();
        file.read_to_string(&mut buffer)?;
        let s = serde_json::to_string(cached_settings)?;
        let s = Sha256::digest_str(&s);
        let encoded = base64::encode(&s);
        if encoded == buffer {
            debug!("Config state matches supplied config.");
            Ok(false)
        } else {
            Ok(true)
        }
    }

    match diff_with_cached_inner(settings, path) {
        Ok(result) => result,

        Err(err) => {
            log_failure(Level::Debug, &err);
            debug!("Error reading config backup.");
            true
        }
    }
}

#[derive(Debug, Fail)]
#[fail(display = "Could not load settings")]
pub struct DiffError(#[cause] Context<Box<dyn std::fmt::Display + Send + Sync>>);

impl From<std::io::Error> for DiffError {
    fn from(err: std::io::Error) -> Self {
        DiffError(Context::new(Box::new(err)))
    }
}

impl From<serde_json::Error> for DiffError {
    fn from(err: serde_json::Error) -> Self {
        DiffError(Context::new(Box::new(err)))
    }
}

fn check_settings_state<M, C>(
    subdir_path: PathBuf,
    filename: &str,
    settings: &M::Settings,
    runtime: &M::ModuleRuntime,
    crypto: &C,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<(), Error>
where
    M: MakeModuleRuntime + 'static,
    M::Settings: Serialize,
    C: CreateCertificate + GetIssuerAlias + MasterEncryptionKey,
{
    info!("Detecting if configuration file has changed...");
    let path = subdir_path.join(filename);
    let mut reconfig_reqd = false;
    let diff = diff_with_cached(settings, &path);
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
        reconfigure::<M, _>(
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
    settings: &M::Settings,
    runtime: &M::ModuleRuntime,
    crypto: &C,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<(), Error>
where
    M: MakeModuleRuntime + 'static,
    M::Settings: Serialize,
    C: CreateCertificate + GetIssuerAlias + MasterEncryptionKey,
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
fn start_api<HC, K, F, C, W, M>(
    settings: &M::Settings,
    hyper_client: HC,
    runtime: &M::ModuleRuntime,
    key_store: &DerivedKeyStore<K>,
    workload_config: W,
    root_key: K,
    shutdown_signal: F,
    crypto: &C,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<StartApiReturnStatus, Error>
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
    M::ModuleRuntime: Authenticator<Request = Request<Body>> + Send + Sync + Clone + 'static,
    M: MakeModuleRuntime + 'static,
    <<M::ModuleRuntime as ModuleRuntime>::Module as Module>::Config:
        Clone + DeserializeOwned + Serialize,
    M::Settings: 'static,
    <M::ModuleRuntime as ModuleRuntime>::Logs: Into<Body>,
    <M::ModuleRuntime as Authenticator>::Error: Fail + Sync,
    for<'r> &'r <M::ModuleRuntime as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
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

    let edgelet_cert_props = CertificateProperties::new(
        IOTEDGED_VALIDITY,
        IOTEDGED_TLS_COMMONNAME.to_string(),
        CertificateType::Server,
        "iotedge-tls".to_string(),
    )
    .with_issuer(CertificateIssuer::DeviceCa);

    let cert_manager = CertificateManager::new(crypto.clone(), edgelet_cert_props).context(
        ErrorKind::Initialize(InitializeErrorReason::CreateCertificateManager),
    )?;

    // Create the certificate management timer and channel
    let (restart_tx, restart_rx) = oneshot::channel();

    let expiration_timer = if settings.listen().management_uri().scheme() == "https"
        || settings.listen().workload_uri().scheme() == "https"
    {
        Either::A(
            cert_manager
                .schedule_expiration_timer(move || restart_tx.send(()))
                .map_err(|err| {
                    Error::from(err.context(ErrorKind::CertificateExpirationManagement))
                }),
        )
    } else {
        Either::B(future::ok(()))
    };

    let cert_manager = Arc::new(cert_manager);

    let mgmt =
        start_management::<_, _, _, M>(settings, runtime, &id_man, mgmt_rx, cert_manager.clone());

    let workload = start_workload::<_, _, _, _, M>(
        settings,
        key_store,
        runtime,
        work_rx,
        crypto,
        cert_manager,
        workload_config,
    );

    let (runt_tx, runt_rx) = oneshot::channel();
    let edge_rt = start_runtime::<_, _, M>(
        runtime.clone(),
        &id_man,
        &hub_name,
        &device_id,
        &settings,
        runt_rx,
    )?;

    // Wait for the watchdog to finish, and then send signal to the workload and management services.
    // This way the edgeAgent can finish shutting down all modules.

    let edge_rt_with_cleanup = edge_rt.select2(restart_rx).then(move |res| {
        mgmt_tx.send(()).unwrap_or(());
        work_tx.send(()).unwrap_or(());

        // A -> EdgeRt Future
        // B -> Restart Signal Future
        match res {
            Ok(Either::A(_)) => Ok(StartApiReturnStatus::Shutdown).into_future(),
            Ok(Either::B(_)) => Ok(StartApiReturnStatus::Restart).into_future(),
            Err(Either::A((err, _))) => Err(err).into_future(),
            Err(Either::B(_)) => {
                debug!("The restart signal failed, shutting down.");
                Ok(StartApiReturnStatus::Shutdown).into_future()
            }
        }
    });

    let shutdown = shutdown_signal.map(move |_| {
        debug!("shutdown signaled");
        // Signal the watchdog to shutdown
        runt_tx.send(()).unwrap_or(());
    });
    tokio_runtime.spawn(shutdown);

    let services = mgmt
        .join4(workload, edge_rt_with_cleanup, expiration_timer)
        .then(|result| match result {
            Ok(((), (), code, ())) => Ok(code),
            Err(err) => Err(err),
        });
    let restart_code = tokio_runtime.block_on(services)?;

    Ok(restart_code)
}

fn init_runtime<M>(
    settings: M::Settings,
    tokio_runtime: &mut tokio::runtime::Runtime,
    provisioning_result: M::ProvisioningResult,
) -> Result<M::ModuleRuntime, Error>
where
    M: MakeModuleRuntime + Send + 'static,
    M::ModuleRuntime: Send,
    M::Future: 'static,
{
    info!("Initializing the module runtime...");
    let runtime = tokio_runtime
        .block_on(M::make_runtime(settings, provisioning_result))
        .context(ErrorKind::Initialize(InitializeErrorReason::ModuleRuntime))?;
    info!("Finished initializing the module runtime.");

    Ok(runtime)
}

fn manual_provision(
    provisioning: &Manual,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<(DerivedKeyStore<MemoryKey>, ProvisioningResult, MemoryKey), Error> {
    let (key, device_id, hub) =
        provisioning
            .parse_device_connection_string()
            .context(ErrorKind::Initialize(
                InitializeErrorReason::ManualProvisioningClient,
            ))?;
    let manual = ManualProvisioning::new(key, device_id, hub);
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

fn external_provision_payload(key: &[u8]) -> (DerivedKeyStore<MemoryKey>, MemoryKey) {
    let memory_key = MemoryKey::new(key);
    let mut memory_hsm = MemoryKeyStore::new();
    memory_hsm.insert(&KeyIdentity::Device, "primary", memory_key.clone());

    let derived_key_store = DerivedKeyStore::new(memory_key.clone());
    (derived_key_store, memory_key)
}

fn external_provision_tpm(
    hsm_lock: Arc<HsmLock>,
) -> Result<(DerivedKeyStore<TpmKey>, TpmKey), Error> {
    let tpm = Tpm::new().context(ErrorKind::Initialize(
        InitializeErrorReason::ExternalProvisioningClient(
            ExternalProvisioningErrorReason::HsmInitialization,
        ),
    ))?;

    let tpm_hsm = TpmKeyStore::from_hsm(tpm, hsm_lock).context(ErrorKind::Initialize(
        InitializeErrorReason::ExternalProvisioningClient(
            ExternalProvisioningErrorReason::HsmInitialization,
        ),
    ))?;

    tpm_hsm
        .get(&KeyIdentity::Device, "primary")
        .map_err(|err| {
            Error::from(err.context(ErrorKind::Initialize(
                InitializeErrorReason::ExternalProvisioningClient(
                    ExternalProvisioningErrorReason::HsmKeyRetrieval,
                ),
            )))
        })
        .and_then(|k| {
            let derived_key_store = DerivedKeyStore::new(k.clone());
            Ok((derived_key_store, k))
        })
}

fn dps_symmetric_key_provision<HC>(
    provisioning: &Dps,
    hyper_client: HC,
    backup_path: PathBuf,
    tokio_runtime: &mut tokio::runtime::Runtime,
    key: &SymmetricKeyAttestationInfo,
) -> Result<(DerivedKeyStore<MemoryKey>, ProvisioningResult, MemoryKey), Error>
where
    HC: 'static + ClientImpl,
{
    let mut memory_hsm = MemoryKeyStore::new();
    let key_bytes =
        base64::decode(key.symmetric_key()).context(ErrorKind::SymmetricKeyMalformed)?;

    memory_hsm
        .activate_identity_key(KeyIdentity::Device, "primary".to_string(), key_bytes)
        .context(ErrorKind::ActivateSymmetricKey)?;

    let dps = DpsSymmetricKeyProvisioning::new(
        hyper_client,
        provisioning.global_endpoint().clone(),
        provisioning.scope_id().to_string(),
        key.registration_id().to_string(),
        DPS_API_VERSION.to_string(),
    )
    .context(ErrorKind::Initialize(
        InitializeErrorReason::DpsProvisioningClient,
    ))?;
    let provision_with_file_backup = BackupProvisioning::new(dps, backup_path);

    let provision =
        provision_with_file_backup
            .provision(memory_hsm.clone())
            .map_err(|err| {
                Error::from(err.context(ErrorKind::Initialize(
                    InitializeErrorReason::DpsProvisioningClient,
                )))
            })
            .and_then(move |prov_result| {
                info!("Successful DPS provisioning.");
                let k = memory_hsm.get(&KeyIdentity::Device, "primary").context(
                    ErrorKind::Initialize(InitializeErrorReason::DpsProvisioningClient),
                )?;
                let derived_key_store = DerivedKeyStore::new(k.clone());
                Ok((derived_key_store, prov_result, k))
            });

    tokio_runtime.block_on(provision)
}

fn dps_tpm_provision<HC>(
    provisioning: &Dps,
    hyper_client: HC,
    backup_path: PathBuf,
    tokio_runtime: &mut tokio::runtime::Runtime,
    tpm_attestation_info: &TpmAttestationInfo,
    hsm_lock: Arc<HsmLock>,
) -> Result<(DerivedKeyStore<TpmKey>, ProvisioningResult, TpmKey), Error>
where
    HC: 'static + ClientImpl,
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
    let dps = DpsTpmProvisioning::new(
        hyper_client,
        provisioning.global_endpoint().clone(),
        provisioning.scope_id().to_string(),
        tpm_attestation_info.registration_id().to_string(),
        DPS_API_VERSION.to_string(),
        ek_result,
        srk_result,
    )
    .context(ErrorKind::Initialize(
        InitializeErrorReason::DpsProvisioningClient,
    ))?;
    let tpm_hsm = TpmKeyStore::from_hsm(tpm, hsm_lock).context(ErrorKind::Initialize(
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
        .and_then(move |prov_result| {
            let k = tpm_hsm
                .get(&KeyIdentity::Device, "primary")
                .context(ErrorKind::Initialize(
                    InitializeErrorReason::DpsProvisioningClient,
                ))?;
            let derived_key_store = DerivedKeyStore::new(k.clone());
            Ok((derived_key_store, prov_result, k))
        });

    tokio_runtime.block_on(provision)
}

fn start_runtime<K, HC, M>(
    runtime: M::ModuleRuntime,
    id_man: &HubIdentityManager<DerivedKeyStore<K>, HC, K>,
    hostname: &str,
    device_id: &str,
    settings: &M::Settings,
    shutdown: Receiver<()>,
) -> Result<impl Future<Item = (), Error = Error>, Error>
where
    K: 'static + Sign + Clone + Send + Sync,
    HC: 'static + ClientImpl,
    M: MakeModuleRuntime,
    M::ModuleRuntime: Clone + 'static,
    <<M::ModuleRuntime as ModuleRuntime>::Module as Module>::Config:
        Clone + DeserializeOwned + Serialize,
    <M::ModuleRuntime as ModuleRuntime>::Logs: Into<Body>,
    for<'r> &'r <M::ModuleRuntime as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
{
    let spec = settings.agent().clone();
    let env = build_env(spec.env(), hostname, device_id, settings);
    let spec = ModuleSpec::<<M::ModuleRuntime as ModuleRuntime>::Config>::new(
        EDGE_RUNTIME_MODULE_NAME.to_string(),
        spec.type_().to_string(),
        spec.config().clone(),
        env,
        spec.image_pull_policy(),
    )
    .context(ErrorKind::Initialize(InitializeErrorReason::EdgeRuntime))?;

    let watchdog = Watchdog::new(
        runtime,
        id_man.clone(),
        settings.watchdog().max_retries().clone(),
    );
    let runtime_future = watchdog
        .run_until(spec, EDGE_RUNTIME_MODULEID, shutdown.map_err(|_| ()))
        .map_err(Error::from);

    Ok(runtime_future)
}

// Add the environment variables needed by the EdgeAgent.
fn build_env<S>(
    spec_env: &HashMap<String, String>,
    hostname: &str,
    device_id: &str,
    settings: &S,
) -> HashMap<String, String>
where
    S: RuntimeSettings,
{
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
    for (key, val) in spec_env.iter() {
        env.insert(key.clone(), val.clone());
    }
    env.insert(API_VERSION_KEY.to_string(), API_VERSION.to_string());
    env
}

fn start_management<C, K, HC, M>(
    settings: &M::Settings,
    runtime: &M::ModuleRuntime,
    id_man: &HubIdentityManager<DerivedKeyStore<K>, HC, K>,
    shutdown: Receiver<()>,
    cert_manager: Arc<CertificateManager<C>>,
) -> impl Future<Item = (), Error = Error>
where
    C: CreateCertificate + Clone,
    K: 'static + Sign + Clone + Send + Sync,
    HC: 'static + ClientImpl + Send + Sync,
    M: MakeModuleRuntime,
    M::ModuleRuntime: Authenticator<Request = Request<Body>> + Send + Sync + Clone + 'static,
    <<M::ModuleRuntime as Authenticator>::AuthenticateFuture as Future>::Error: Fail,
    for<'r> &'r <M::ModuleRuntime as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
    <<M::ModuleRuntime as ModuleRuntime>::Module as Module>::Config: DeserializeOwned + Serialize,
    <M::ModuleRuntime as ModuleRuntime>::Logs: Into<Body>,
{
    info!("Starting management API...");

    let label = "mgmt".to_string();
    let url = settings.listen().management_uri().clone();

    ManagementService::new(runtime, id_man)
        .then(move |service| -> Result<_, Error> {
            let service = service.context(ErrorKind::Initialize(
                InitializeErrorReason::ManagementService,
            ))?;
            let service = LoggingService::new(label, service);

            let run = Http::new()
                .bind_url(url.clone(), service, Some(&cert_manager))
                .map_err(|err| {
                    err.context(ErrorKind::Initialize(
                        InitializeErrorReason::ManagementService,
                    ))
                })?
                .run_until(shutdown.map_err(|_| ()))
                .map_err(|err| Error::from(err.context(ErrorKind::ManagementService)));
            info!("Listening on {} with 1 thread for management API.", url);
            Ok(run)
        })
        .flatten()
}

fn start_workload<K, C, CE, W, M>(
    settings: &M::Settings,
    key_store: &K,
    runtime: &M::ModuleRuntime,
    shutdown: Receiver<()>,
    crypto: &C,
    cert_manager: Arc<CertificateManager<CE>>,
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
    CE: CreateCertificate + Clone,
    W: WorkloadConfig + Clone + Send + Sync + 'static,
    M: MakeModuleRuntime + 'static,
    M::Settings: 'static,
    M::ModuleRuntime: 'static + Authenticator<Request = Request<Body>> + Clone + Send + Sync,
    <<M::ModuleRuntime as Authenticator>::AuthenticateFuture as Future>::Error: Fail,
    for<'r> &'r <M::ModuleRuntime as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
    <<M::ModuleRuntime as ModuleRuntime>::Module as Module>::Config:
        Clone + DeserializeOwned + Serialize,
    <M::ModuleRuntime as ModuleRuntime>::Logs: Into<Body>,
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
                .bind_url(url.clone(), service, Some(&cert_manager))
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
    use std::path::Path;

    use chrono::{Duration, Utc};
    use tempdir::TempDir;

    use edgelet_core::ModuleRuntimeState;
    use edgelet_core::{KeyBytes, PrivateKey};
    use edgelet_docker::{DockerConfig, DockerModuleRuntime, Settings};
    use edgelet_test_utils::cert::TestCert;
    use edgelet_test_utils::module::*;

    use super::*;
    use docker::models::ContainerCreateBody;

    #[cfg(unix)]
    static GOOD_SETTINGS: &str = "../edgelet-docker/test/linux/sample_settings.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS1: &str = "test/linux/sample_settings1.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS2: &str = "test/linux/sample_settings2.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_DPS_TPM1: &str = "test/linux/sample_settings.dps.tpm.1.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_DPS_DEFAULT: &str =
        "../edgelet-docker/test/linux/sample_settings.dps.default.yaml";

    #[cfg(windows)]
    static GOOD_SETTINGS: &str = "../edgelet-docker/test/windows/sample_settings.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS1: &str = "test/windows/sample_settings1.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS2: &str = "test/windows/sample_settings2.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_DPS_TPM1: &str = "test/windows/sample_settings.dps.tpm.1.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_DPS_DEFAULT: &str =
        "../edgelet-docker/test/windows/sample_settings.dps.default.yaml";

    #[derive(Clone, Copy, Debug, Fail)]
    pub struct Error;

    impl fmt::Display for Error {
        fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
            write!(f, "Error")
        }
    }

    struct TestCrypto {
        use_expired_ca: bool,
        fail_device_ca_alias: bool,
    }

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

        fn get_certificate(
            &self,
            _alias: String,
        ) -> Result<Self::Certificate, edgelet_core::Error> {
            let ts = if self.use_expired_ca {
                Utc::now()
            } else {
                Utc::now() + Duration::hours(1)
            };
            Ok(TestCert::default()
                .with_cert(vec![1, 2, 3])
                .with_private_key(PrivateKey::Key(KeyBytes::Pem("some key".to_string())))
                .with_fail_pem(false)
                .with_fail_private_key(false)
                .with_valid_to(ts))
        }
    }

    impl GetIssuerAlias for TestCrypto {
        fn get_issuer_alias(
            &self,
            _issuer: CertificateIssuer,
        ) -> Result<String, edgelet_core::Error> {
            if self.fail_device_ca_alias {
                Err(edgelet_core::Error::from(
                    edgelet_core::ErrorKind::InvalidIssuer,
                ))
            } else {
                Ok("test-device-ca".to_string())
            }
        }
    }

    #[test]
    fn default_settings_raise_unconfigured_error() {
        let settings = Settings::new(None).unwrap();
        let main = Main::<DockerModuleRuntime>::new(settings);
        let result = main.run_until(signal::shutdown);
        match result.unwrap_err().kind() {
            ErrorKind::Initialize(InitializeErrorReason::NotConfigured) => (),
            kind => panic!("Expected `NotConfigured` but got {:?}", kind),
        }
    }

    #[test]
    fn settings_with_invalid_issuer_ca_fails() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS))).unwrap();
        let config = DockerConfig::new(
            "microsoft/test-image".to_string(),
            ContainerCreateBody::new(),
            None,
        )
        .unwrap();
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error, _> =
            TestModule::new_with_config("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::make_runtime(settings.clone(), TestProvisioningResult::new())
            .wait()
            .unwrap()
            .with_module(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: true,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        let result = check_settings_state::<TestRuntime<_, Settings>, _>(
            tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
        );
        match result.unwrap_err().kind() {
            ErrorKind::Initialize(InitializeErrorReason::PrepareWorkloadCa) => (),
            kind => panic!("Expected `PrepareWorkloadCa` but got {:?}", kind),
        }
    }

    #[test]
    fn settings_with_expired_issuer_ca_fails() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS))).unwrap();
        let config = DockerConfig::new(
            "microsoft/test-image".to_string(),
            ContainerCreateBody::new(),
            None,
        )
        .unwrap();
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error, _> =
            TestModule::new_with_config("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::make_runtime(settings.clone(), TestProvisioningResult::new())
            .wait()
            .unwrap()
            .with_module(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: true,
            fail_device_ca_alias: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        let result = check_settings_state::<TestRuntime<_, Settings>, _>(
            tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
        );
        match result.unwrap_err().kind() {
            ErrorKind::Initialize(InitializeErrorReason::IssuerCAExpiration) => (),
            kind => panic!("Expected `IssuerCAExpiration` but got {:?}", kind),
        }
    }

    #[test]
    fn settings_first_time_creates_backup() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS))).unwrap();
        let config = DockerConfig::new(
            "microsoft/test-image".to_string(),
            ContainerCreateBody::new(),
            None,
        )
        .unwrap();
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error, _> =
            TestModule::new_with_config("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::make_runtime(settings.clone(), TestProvisioningResult::new())
            .wait()
            .unwrap()
            .with_module(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        check_settings_state::<TestRuntime<_, Settings>, _>(
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
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS))).unwrap();
        let config = DockerConfig::new(
            "microsoft/test-image".to_string(),
            ContainerCreateBody::new(),
            None,
        )
        .unwrap();
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error, _> =
            TestModule::new_with_config("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::make_runtime(settings.clone(), TestProvisioningResult::new())
            .wait()
            .unwrap()
            .with_module(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        check_settings_state::<TestRuntime<_, Settings>, _>(
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

        let settings1 = Settings::new(Some(Path::new(GOOD_SETTINGS1))).unwrap();
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        check_settings_state::<TestRuntime<_, Settings>, _>(
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

    #[test]
    fn diff_with_same_cached_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS))).unwrap();
        let settings_to_write = serde_json::to_string(&settings).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        File::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        assert!(!diff_with_cached(&settings, &path));
    }

    #[test]
    fn diff_with_tpm_default_and_explicit_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 = Settings::new(Some(Path::new(GOOD_SETTINGS_DPS_DEFAULT))).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        File::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS_DPS_TPM1))).unwrap();
        assert!(!diff_with_cached(&settings, &path));
    }

    #[test]
    fn diff_with_tpm_explicit_and_default_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 = Settings::new(Some(Path::new(GOOD_SETTINGS_DPS_TPM1))).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        File::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS_DPS_DEFAULT))).unwrap();
        assert!(!diff_with_cached(&settings, &path));
    }

    #[test]
    fn diff_with_same_cached_env_var_unordered_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 = Settings::new(Some(Path::new(GOOD_SETTINGS2))).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        File::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS))).unwrap();
        assert!(!diff_with_cached(&settings, &path));
    }

    #[test]
    fn diff_with_different_cached_returns_true() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 = Settings::new(Some(Path::new(GOOD_SETTINGS1))).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        File::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS))).unwrap();
        assert!(diff_with_cached(&settings, &path));
    }

    #[test]
    fn diff_with_no_file_returns_true() {
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS))).unwrap();
        assert!(diff_with_cached(&settings, Path::new("i dont exist")));
    }
}
