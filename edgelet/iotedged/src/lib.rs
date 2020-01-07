// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::doc_markdown, // clippy want the "IoT" of "IoT Hub" in a code fence
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::shadow_unrelated,
    clippy::too_many_lines,
    clippy::type_complexity,
    clippy::use_self
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

use futures::sync::mpsc;
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
use futures::{future, Future, Stream};
use hyper::server::conn::Http;
use hyper::{Body, Request, Uri};
use log::{debug, info, Level};
use serde::de::DeserializeOwned;
use serde::Serialize;
use sha2::{Digest, Sha256};
use url::Url;

use dps::DPS_API_VERSION;
use edgelet_core::crypto::{
    Activate, CreateCertificate, Decrypt, DerivedKeyStore, Encrypt, GetDeviceIdentityCertificate,
    GetHsmVersion, GetIssuerAlias, GetTrustBundle, KeyIdentity, KeyStore, MakeRandom,
    MasterEncryptionKey, MemoryKey, MemoryKeyStore, Sign, Signature, SignatureAlgorithm,
    IOTEDGED_CA_ALIAS,
};
use edgelet_core::watchdog::Watchdog;
use edgelet_core::{
    AttestationMethod, Authenticator, Certificate, CertificateIssuer, CertificateProperties,
    CertificateType, Dps, MakeModuleRuntime, ManualAuthMethod, Module, ModuleRuntime,
    ModuleRuntimeErrorReason, ModuleSpec, ProvisioningResult as CoreProvisioningResult,
    ProvisioningType, RuntimeSettings, SymmetricKeyAttestationInfo, TpmAttestationInfo,
    WorkloadConfig, X509AttestationInfo,
};
use edgelet_hsm::tpm::{TpmKey, TpmKeyStore};
use edgelet_hsm::{Crypto, HsmLock, X509};
use edgelet_http::certificate_manager::CertificateManager;
use edgelet_http::client::{Client as HttpClient, ClientImpl};
use edgelet_http::logging::LoggingService;
use edgelet_http::{HyperExt, MaybeProxyClient, PemCertificate, TlsAcceptorParams, API_VERSION};
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
    AuthType, BackupProvisioning, CredentialSource, DpsSymmetricKeyProvisioning,
    DpsTpmProvisioning, DpsX509Provisioning, ExternalProvisioning, ManualProvisioning, Provision,
    ProvisioningResult, ReprovisioningStatus,
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

/// This is the edge runtime mode - it should be iotedged, when iotedged starts edge runtime in single node mode.
#[cfg(feature = "runtime-docker")]
const EDGE_RUNTIME_MODE: &str = "iotedged";

/// This is the edge runtime mode - it should be kubernetes, when iotedged starts edge runtime in kubernetes mode.
#[cfg(feature = "runtime-kubernetes")]
const EDGE_RUNTIME_MODE: &str = "kubernetes";

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

/// This is the name of the hybrid id subdirectory that will
/// contain the hybrid key and other related files
const EDGE_HYBRID_IDENTITY_SUBDIR: &str = "hybrid_id";

/// This is the name of the hybrid X509-SAS key file
const EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME: &str = "iotedge_hybrid_key";
/// This is the name of the hybrid X509-SAS initialization vector
const EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME: &str = "iotedge_hybrid_iv";

/// This is the name of the external provisioning subdirectory that will
/// contain the device's identity certificate, private key and other related files
const EDGE_EXTERNAL_PROVISIONING_SUBDIR: &str = "external_prov";

/// This is the name of the identity X509 certificate file
const EDGE_EXTERNAL_PROVISIONING_ID_CERT_FILENAME: &str = "id_cert";
/// This is the name of the identity X509 private key file
const EDGE_EXTERNAL_PROVISIONING_ID_KEY_FILENAME: &str = "id_key";

/// Size in bytes of the master identity key
/// The length has been chosen to be compliant with the underlying
/// default implementation of the HSM lib encryption algorithm. In the future
/// should this need to change, both IDENTITY_MASTER_KEY_LEN_BYTES and
/// IOTEDGED_CRYPTO_IV_LEN_BYTES lengths must be considered and modified appropriately.
const IDENTITY_MASTER_KEY_LEN_BYTES: usize = 32;
/// Size in bytes of the initialization vector
/// The length has been chosen to be compliant with the underlying
/// default implementation of the HSM lib encryption algorithm. In the future
/// should this need to change, both IDENTITY_MASTER_KEY_LEN_BYTES and
/// IOTEDGED_CRYPTO_IV_LEN_BYTES lengths must be considered and modified appropriately.
const IOTEDGED_CRYPTO_IV_LEN_BYTES: usize = 16;
/// Identity to be used for various crypto operations
const IOTEDGED_CRYPTO_ID: &str = "$iotedge";

/// This is the name of the cache subdirectory for settings state
const EDGE_SETTINGS_SUBDIR: &str = "cache";

/// This is the DPS registration ID env variable key
const DPS_REGISTRATION_ID_ENV_KEY: &str = "IOTEDGE_REGISTRATION_ID";

/// This is the edge device identity certificate file path env variable key.
/// This is used for both DPS attestation and manual authentication modes.
const DEVICE_IDENTITY_CERT_PATH_ENV_KEY: &str = "IOTEDGE_DEVICE_IDENTITY_CERT";
/// This is the edge device identity private key file path env variable key.
/// This is used for both DPS attestation and manual authentication modes.
const DEVICE_IDENTITY_KEY_PATH_ENV_KEY: &str = "IOTEDGE_DEVICE_IDENTITY_PK";

const IOTEDGED_COMMONNAME: &str = "iotedged workload ca";
const IOTEDGED_TLS_COMMONNAME: &str = "iotedged";
// 5 mins
const IOTEDGED_MIN_EXPIRATION_DURATION: i64 = 5 * 60;
// 2 hours
const IOTEDGE_ID_CERT_MAX_DURATION_SECS: i64 = 2 * 3600;
// 90 days
const IOTEDGE_SERVER_CERT_MAX_DURATION_SECS: i64 = 90 * 24 * 3600;

// HSM lib version that the iotedge runtime required
const IOTEDGE_COMPAT_HSM_VERSION: &str = "1.0.3";

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

#[derive(Debug, PartialEq)]
enum ProvisioningAuthMethod {
    X509,
    SharedAccessKey,
}

#[derive(Debug)]
struct IdentityCertificateData {
    common_name: String,
    thumbprint: String,
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

        let (external_provisioning_info, external_provisioning) =
            get_external_provisioning_info(&settings, &mut tokio_runtime)?;

        set_iot_edge_env_vars(&settings, &external_provisioning_info)
            .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;

        let auto_generated_ca_lifetime_seconds =
            settings.certificates().auto_generated_ca_lifetime_seconds();

        info!("Initializing hsm...");
        let crypto = Crypto::new(hsm_lock.clone(), auto_generated_ca_lifetime_seconds)
            .context(ErrorKind::Initialize(InitializeErrorReason::Hsm))?;

        let hsm_version = crypto
            .get_version()
            .context(ErrorKind::Initialize(InitializeErrorReason::Hsm))?;

        if hsm_version != IOTEDGE_COMPAT_HSM_VERSION {
            info!(
                "Incompatible HSM crypto interface version. Found {}, required {}",
                hsm_version, IOTEDGE_COMPAT_HSM_VERSION
            );
            return Err(Error::from(ErrorKind::Initialize(
                InitializeErrorReason::IncompatibleHsmVersion,
            )));
        }

        // ensure a master encryption key is initialized
        crypto.create_key().context(ErrorKind::Initialize(
            InitializeErrorReason::CreateMasterEncryptionKey,
        ))?;
        info!("Finished initializing hsm.");

        let (hyper_client, device_cert_identity_data) = prepare_httpclient_and_identity_data(
            hsm_lock.clone(),
            &settings,
            external_provisioning_info.as_ref(),
            auto_generated_ca_lifetime_seconds,
        )?;

        let cache_subdir_path = Path::new(&settings.homedir()).join(EDGE_SETTINGS_SUBDIR);
        // make sure the cache directory exists
        DirBuilder::new()
            .recursive(true)
            .create(&cache_subdir_path)
            .context(ErrorKind::Initialize(
                InitializeErrorReason::CreateCacheDirectory,
            ))?;

        macro_rules! start_edgelet {
            ($key_store:ident, $provisioning_result:ident, $root_key:ident, $force_reprovision:ident, $id_cert_thumprint:ident, $provision:ident,) => {{
                info!("Finished provisioning edge device.");

                let runtime = init_runtime::<M>(
                    settings.clone(),
                    &mut tokio_runtime,
                    $provisioning_result.clone(),
                    crypto.clone(),
                )?;

                if $force_reprovision ||
                    ($provisioning_result.reconfigure() != ReprovisioningStatus::DeviceDataNotUpdated) {
                    // If this device was re-provisioned and the device key was updated it causes
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
                    &cache_subdir_path,
                    EDGE_SETTINGS_STATE_FILENAME,
                    &settings,
                    &runtime,
                    &crypto,
                    &mut tokio_runtime,
                    $id_cert_thumprint,
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
                    let (code, should_reprovision) = start_api::<_, _, _, _, _, M>(
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

                    if should_reprovision {
                        let reprovision = $provision.reprovision().map_err(|err| {
                            return Error::from(err.context(ErrorKind::ReprovisionFailure))
                        });

                        tokio_runtime.block_on(reprovision)?;

                        // Return an error here to let the daemon exit with an error code.
                        // This will make `systemd` restart the daemon which will re-execute the
                        // provisioning flow and if the device has been re-provisioned, the daemon
                        // will configure itself with the new provisioning information as part of
                        // that flow.
                        return Err(Error::from(ErrorKind::DeviceDeprovisioned))
                    }

                    if code != StartApiReturnStatus::Restart {
                        break;
                    }
                }
            }};
        }

        info!("Provisioning edge device...");
        let hybrid_id_subdir_path =
            Path::new(&settings.homedir()).join(EDGE_HYBRID_IDENTITY_SUBDIR);
        let (force_module_reprovision, hybrid_identity_key) = prepare_master_hybrid_identity_key(
            &settings,
            &crypto,
            &hybrid_id_subdir_path,
            EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME,
            EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME,
            external_provisioning_info.as_ref(),
        )?;

        match settings.provisioning().provisioning_type() {
            ProvisioningType::Manual(manual) => {
                match manual.authentication_method() {
                    ManualAuthMethod::DeviceConnectionString(cs) => {
                        info!("Starting provisioning edge device via manual mode using a device connection string...");
                        let (key, device_id, hub) = cs
                            .parse_device_connection_string()
                            .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;
                        let manual = ManualProvisioning::new(key, device_id, hub);

                        let (key_store, provisioning_result, root_key) =
                            manual_provision_connection_string(&manual, &mut tokio_runtime)?;

                        start_edgelet!(
                            key_store,
                            provisioning_result,
                            root_key,
                            force_module_reprovision,
                            None,
                            manual,
                        );
                    }
                    ManualAuthMethod::X509(x509) => {
                        info!("Starting provisioning edge device via manual mode using X509 identity certificate...");

                        let id_data = device_cert_identity_data.ok_or_else(|| {
                            ErrorKind::Initialize(InitializeErrorReason::ManualProvisioningClient)
                        })?;

                        let key_bytes = hybrid_identity_key.ok_or_else(|| {
                            ErrorKind::Initialize(InitializeErrorReason::ManualProvisioningClient)
                        })?;

                        let manual = ManualProvisioning::new(
                            MemoryKey::new(key_bytes),
                            x509.device_id().to_string(),
                            x509.iothub_hostname().to_string(),
                        );
                        let (key_store, provisioning_result, root_key) = manual_provision_x509(
                            &manual,
                            &mut tokio_runtime,
                            id_data.thumbprint.clone(),
                        )?;
                        let thumbprint_op = Some(id_data.thumbprint.as_str());
                        start_edgelet!(
                            key_store,
                            provisioning_result,
                            root_key,
                            force_module_reprovision,
                            thumbprint_op,
                            manual,
                        );
                    }
                };
            }
            ProvisioningType::External(_external) => {
                info!("Starting provisioning edge device via external provisioning mode...");
                let provisioning_result = external_provisioning_info.ok_or_else(|| {
                    ErrorKind::Initialize(InitializeErrorReason::ExternalProvisioningClient(
                        ExternalProvisioningErrorReason::Provisioning,
                    ))
                })?;

                let credentials = if let Some(credentials) = provisioning_result.credentials() {
                    credentials
                } else {
                    info!("Credentials are expected to be populated for external provisioning.");

                    return Err(Error::from(ErrorKind::Initialize(
                        InitializeErrorReason::ExternalProvisioningClient(
                            ExternalProvisioningErrorReason::InvalidCredentials,
                        ),
                    )));
                };

                let external_provisioning_val = external_provisioning.ok_or_else(|| {
                    ErrorKind::Initialize(InitializeErrorReason::ExternalProvisioningClient(
                        ExternalProvisioningErrorReason::Provisioning,
                    ))
                })?;
                match credentials.auth_type() {
                    AuthType::SymmetricKey(symmetric_key) => {
                        if let Some(key) = symmetric_key.key() {
                            let (derived_key_store, memory_key) = external_provision_payload(key);
                            start_edgelet!(
                                derived_key_store,
                                provisioning_result,
                                memory_key,
                                force_module_reprovision,
                                None,
                                external_provisioning_val,
                            );
                        } else {
                            let (derived_key_store, tpm_key) = external_provision_tpm(hsm_lock)?;
                            start_edgelet!(
                                derived_key_store,
                                provisioning_result,
                                tpm_key,
                                force_module_reprovision,
                                None,
                                external_provisioning_val,
                            );
                        }
                    }
                    AuthType::X509(_x509) => {
                        let id_data = device_cert_identity_data.ok_or_else(|| {
                            ErrorKind::Initialize(InitializeErrorReason::DpsProvisioningClient)
                        })?;

                        let key_bytes = hybrid_identity_key.ok_or_else(|| {
                            ErrorKind::Initialize(InitializeErrorReason::DpsProvisioningClient)
                        })?;

                        let thumbprint_op = Some(id_data.thumbprint.as_str());
                        let (key_store, root_key) = external_provision_x509(
                            &provisioning_result,
                            &key_bytes,
                            id_data.thumbprint.as_str(),
                        )?;

                        start_edgelet!(
                            key_store,
                            provisioning_result,
                            root_key,
                            force_module_reprovision,
                            thumbprint_op,
                            external_provisioning_val,
                        );
                    }
                };
            }
            ProvisioningType::Dps(dps) => {
                let dps_path = cache_subdir_path.join(EDGE_PROVISIONING_BACKUP_FILENAME);

                match dps.attestation() {
                    AttestationMethod::Tpm(ref tpm) => {
                        info!("Starting provisioning edge device via TPM...");
                        let (tpm_instance, dps_tpm) =
                            dps_tpm_provision_init(&dps, hyper_client.clone(), tpm)?;
                        let (key_store, provisioning_result, root_key) = dps_tpm_provision(
                            dps_path,
                            &mut tokio_runtime,
                            hsm_lock,
                            tpm_instance,
                            &dps_tpm,
                        )?;

                        start_edgelet!(
                            key_store,
                            provisioning_result,
                            root_key,
                            force_module_reprovision,
                            None,
                            dps_tpm,
                        );
                    }
                    AttestationMethod::SymmetricKey(ref symmetric_key_info) => {
                        info!("Starting provisioning edge device via symmetric key...");
                        let (memory_hsm, dps_symmetric_key) = dps_symmetric_key_provision_init(
                            &dps,
                            hyper_client.clone(),
                            symmetric_key_info,
                        )?;
                        let (key_store, provisioning_result, root_key) =
                            dps_symmetric_key_provision(
                                dps_path,
                                &mut tokio_runtime,
                                memory_hsm,
                                &dps_symmetric_key,
                            )?;

                        start_edgelet!(
                            key_store,
                            provisioning_result,
                            root_key,
                            force_module_reprovision,
                            None,
                            dps_symmetric_key,
                        );
                    }
                    AttestationMethod::X509(ref x509_info) => {
                        info!("Starting provisioning edge device via X509 provisioning...");
                        let id_data = device_cert_identity_data.ok_or_else(|| {
                            ErrorKind::Initialize(InitializeErrorReason::DpsProvisioningClient)
                        })?;

                        let (memory_hsm, dps_x509) = dps_x509_provision_init(
                            &dps,
                            hyper_client.clone(),
                            x509_info,
                            hybrid_identity_key,
                            &id_data.common_name,
                        )?;

                        let (key_store, provisioning_result, root_key) = dps_x509_provision(
                            memory_hsm,
                            &dps_x509,
                            dps_path,
                            &mut tokio_runtime,
                            id_data.thumbprint.clone(),
                        )?;
                        let thumbprint_op = Some(id_data.thumbprint.as_str());
                        start_edgelet!(
                            key_store,
                            provisioning_result,
                            root_key,
                            force_module_reprovision,
                            thumbprint_op,
                            dps_x509,
                        );
                    }
                }
            }
        };

        info!("Shutdown complete.");
        Ok(())
    }
}

type ExternalProvisioningInfo = (
    Option<ProvisioningResult>,
    Option<ExternalProvisioning<ExternalProvisioningClient, MemoryKeyStore>>,
);

fn get_external_provisioning_info<S>(
    settings: &S,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<ExternalProvisioningInfo, Error>
where
    S: RuntimeSettings,
{
    if let ProvisioningType::External(external) = settings.provisioning().provisioning_type() {
        // Set the external provisioning endpoint environment variable for use by the custom HSM library.
        env::set_var(
            EXTERNAL_PROVISIONING_ENDPOINT_KEY,
            external.endpoint().as_str(),
        );

        let external_provisioning_client = ExternalProvisioningClient::new(external.endpoint())
            .context(ErrorKind::Initialize(
                InitializeErrorReason::ExternalProvisioningClient(
                    ExternalProvisioningErrorReason::ClientInitialization,
                ),
            ))?;
        let external_provisioning = ExternalProvisioning::new(external_provisioning_client);

        info!("Retrieving provisioning information from the external endpoint...");
        let provision_fut = external_provisioning
            .provision(MemoryKeyStore::new())
            .map_err(|err| {
                Error::from(err.context(ErrorKind::Initialize(
                    InitializeErrorReason::ExternalProvisioningClient(
                        ExternalProvisioningErrorReason::Provisioning,
                    ),
                )))
            });

        let prov_info = tokio_runtime.block_on(provision_fut)?;
        configure_external_provisioning(&prov_info, settings)?;
        Ok((Some(prov_info), Some(external_provisioning)))
    } else {
        Ok((None, None))
    }
}

fn configure_external_provisioning<S>(
    provisioning_info: &ProvisioningResult,
    settings: &S,
) -> Result<(), Error>
where
    S: RuntimeSettings,
{
    if let Some(credentials) = provisioning_info.credentials() {
        if let CredentialSource::Payload = credentials.source() {
            if let AuthType::X509(x509) = credentials.auth_type() {
                let subdir_path =
                    Path::new(&settings.homedir()).join(EDGE_EXTERNAL_PROVISIONING_SUBDIR);

                // Ignore errors from this operation because we could be recovering from a previous bad
                // configuration and shouldn't stall the current configuration because of that
                let _u = fs::remove_dir_all(&subdir_path);
                DirBuilder::new()
                    .recursive(true)
                    .create(&subdir_path)
                    .context(ErrorKind::Initialize(
                        InitializeErrorReason::ExternalProvisioningClient(
                            ExternalProvisioningErrorReason::ExternalProvisioningDirCreate,
                        ),
                    ))?;

                let cert_bytes = base64::decode(x509.identity_cert()).context(
                    ErrorKind::Initialize(InitializeErrorReason::ExternalProvisioningClient(
                        ExternalProvisioningErrorReason::DownloadIdentityCertificate,
                    )),
                )?;
                let pk_bytes = base64::decode(x509.identity_private_key()).context(
                    ErrorKind::Initialize(InitializeErrorReason::ExternalProvisioningClient(
                        ExternalProvisioningErrorReason::DownloadIdentityPrivateKey,
                    )),
                )?;

                let path = subdir_path.join(EDGE_EXTERNAL_PROVISIONING_ID_CERT_FILENAME);
                let mut file = File::create(path).context(ErrorKind::Initialize(
                    InitializeErrorReason::ExternalProvisioningClient(
                        ExternalProvisioningErrorReason::DownloadIdentityCertificate,
                    ),
                ))?;
                file.write_all(&cert_bytes).context(ErrorKind::Initialize(
                    InitializeErrorReason::ExternalProvisioningClient(
                        ExternalProvisioningErrorReason::DownloadIdentityCertificate,
                    ),
                ))?;

                let path = subdir_path.join(EDGE_EXTERNAL_PROVISIONING_ID_KEY_FILENAME);
                let mut file = File::create(path).context(ErrorKind::Initialize(
                    InitializeErrorReason::ExternalProvisioningClient(
                        ExternalProvisioningErrorReason::DownloadIdentityPrivateKey,
                    ),
                ))?;
                file.write_all(&pk_bytes).context(ErrorKind::Initialize(
                    InitializeErrorReason::ExternalProvisioningClient(
                        ExternalProvisioningErrorReason::DownloadIdentityPrivateKey,
                    ),
                ))?;
            }
        }
    };

    Ok(())
}

fn set_iot_edge_env_vars<S>(
    settings: &S,
    provisioning_result: &Option<ProvisioningResult>,
) -> Result<(), Error>
where
    S: RuntimeSettings,
{
    info!(
        "Configuring {} as the home directory.",
        settings.homedir().display()
    );
    env::set_var(HOMEDIR_KEY, &settings.homedir());

    info!("Configuring certificates...");
    let certificates = settings.certificates();

    match certificates.device_cert().as_ref() {
        None => {
            info!("Transparent gateway certificates not found, operating in quick start mode...")
        }
        Some(&c) => {
            let path = c.device_ca_cert().context(ErrorKind::Initialize(
                InitializeErrorReason::CertificateSettings,
            ))?;
            info!(
                "Configuring the Device CA certificate using {:?}.",
                path.as_os_str()
            );
            env::set_var(DEVICE_CA_CERT_KEY, path);

            let path = c.device_ca_pk().context(ErrorKind::Initialize(
                InitializeErrorReason::CertificateSettings,
            ))?;
            info!(
                "Configuring the Device private key using {:?}.",
                path.as_os_str()
            );
            env::set_var(DEVICE_CA_PK_KEY, path);

            let path = c.trusted_ca_certs().context(ErrorKind::Initialize(
                InitializeErrorReason::CertificateSettings,
            ))?;
            info!(
                "Configuring the trusted CA certificates using {:?}.",
                path.as_os_str()
            );
            env::set_var(TRUSTED_CA_CERTS_KEY, path);
        }
    };

    match settings.provisioning().provisioning_type() {
        ProvisioningType::Manual(manual) => {
            if let ManualAuthMethod::X509(x509) = manual.authentication_method() {
                let path = x509.identity_cert().context(ErrorKind::Initialize(
                    InitializeErrorReason::IdentityCertificateSettings,
                ))?;
                env::set_var(DEVICE_IDENTITY_CERT_PATH_ENV_KEY, path.as_os_str());

                let path = x509.identity_pk().context(ErrorKind::Initialize(
                    InitializeErrorReason::IdentityCertificateSettings,
                ))?;
                env::set_var(DEVICE_IDENTITY_KEY_PATH_ENV_KEY, path.as_os_str());
            }
        }
        ProvisioningType::External(_external) => {
            let prov_result = provisioning_result.as_ref().ok_or_else(|| {
                ErrorKind::Initialize(InitializeErrorReason::ExternalProvisioningClient(
                    ExternalProvisioningErrorReason::Provisioning,
                ))
            })?;

            if let Some(credentials) = prov_result.credentials() {
                if let AuthType::X509(x509) = credentials.auth_type() {
                    match credentials.source() {
                        CredentialSource::Hsm => {
                            env::set_var(DEVICE_IDENTITY_CERT_PATH_ENV_KEY, x509.identity_cert());
                            env::set_var(
                                DEVICE_IDENTITY_KEY_PATH_ENV_KEY,
                                x509.identity_private_key(),
                            );
                        }
                        CredentialSource::Payload => {
                            let external_prov_subdir_path = Path::new(&settings.homedir())
                                .join(EDGE_EXTERNAL_PROVISIONING_SUBDIR);
                            let cert_path = external_prov_subdir_path
                                .join(EDGE_EXTERNAL_PROVISIONING_ID_CERT_FILENAME);
                            let key_path = external_prov_subdir_path
                                .join(EDGE_EXTERNAL_PROVISIONING_ID_KEY_FILENAME);

                            env::set_var(DEVICE_IDENTITY_CERT_PATH_ENV_KEY, cert_path.as_os_str());
                            env::set_var(DEVICE_IDENTITY_KEY_PATH_ENV_KEY, key_path.as_os_str());
                        }
                    }
                }
            }
        }
        ProvisioningType::Dps(dps) => match dps.attestation() {
            AttestationMethod::Tpm(ref tpm) => {
                env::set_var(
                    DPS_REGISTRATION_ID_ENV_KEY,
                    tpm.registration_id().to_string(),
                );
            }
            AttestationMethod::SymmetricKey(ref symmetric_key_info) => {
                env::set_var(
                    DPS_REGISTRATION_ID_ENV_KEY,
                    symmetric_key_info.registration_id().to_string(),
                );
            }
            AttestationMethod::X509(ref x509_info) => {
                if let Some(val) = x509_info.registration_id() {
                    env::set_var(DPS_REGISTRATION_ID_ENV_KEY, val.to_string());
                }

                let path = x509_info.identity_cert().context(ErrorKind::Initialize(
                    InitializeErrorReason::IdentityCertificateSettings,
                ))?;
                env::set_var(DEVICE_IDENTITY_CERT_PATH_ENV_KEY, path.as_os_str());

                let path = x509_info.identity_pk().context(ErrorKind::Initialize(
                    InitializeErrorReason::IdentityCertificateSettings,
                ))?;
                env::set_var(DEVICE_IDENTITY_KEY_PATH_ENV_KEY, path.as_os_str());
            }
        },
    }

    info!("Finished configuring provisioning environment variables and certificates.");
    Ok(())
}

fn prepare_httpclient_and_identity_data<S>(
    hsm_lock: Arc<HsmLock>,
    settings: &S,
    provisioning_result: Option<&ProvisioningResult>,
    auto_generated_ca_lifetime_seconds: u64,
) -> Result<(MaybeProxyClient, Option<IdentityCertificateData>), Error>
where
    S: RuntimeSettings,
{
    if get_provisioning_auth_method(settings, provisioning_result)? == ProvisioningAuthMethod::X509
    {
        prepare_httpclient_and_identity_data_for_x509_provisioning(
            hsm_lock,
            auto_generated_ca_lifetime_seconds,
        )
    } else {
        let hyper_client = MaybeProxyClient::new(get_proxy_uri(None)?, None, None)
            .context(ErrorKind::Initialize(InitializeErrorReason::HttpClient))?;

        Ok((hyper_client, None))
    }
}

fn prepare_httpclient_and_identity_data_for_x509_provisioning(
    hsm_lock: Arc<HsmLock>,
    auto_generated_ca_lifetime_seconds: u64,
) -> Result<(MaybeProxyClient, Option<IdentityCertificateData>), Error> {
    info!("Initializing hsm X509 interface...");
    let x509 = X509::new(hsm_lock, auto_generated_ca_lifetime_seconds)
        .context(ErrorKind::Initialize(InitializeErrorReason::Hsm))?;

    let hsm_version = x509
        .get_version()
        .context(ErrorKind::Initialize(InitializeErrorReason::Hsm))?;

    if hsm_version != IOTEDGE_COMPAT_HSM_VERSION {
        info!(
            "Incompatible HSM X.509 identity interface version. Found {}, required {}",
            hsm_version, IOTEDGE_COMPAT_HSM_VERSION
        );
        return Err(Error::from(ErrorKind::Initialize(
            InitializeErrorReason::IncompatibleHsmVersion,
        )));
    }

    let device_identity_cert = x509
        .get()
        .context(ErrorKind::Initialize(InitializeErrorReason::Hsm))?;

    let common_name = device_identity_cert
        .get_common_name()
        .context(ErrorKind::Initialize(
            InitializeErrorReason::InvalidDeviceCertCredentials,
        ))?;

    let thumbprint = get_thumbprint(&device_identity_cert).context(ErrorKind::Initialize(
        InitializeErrorReason::InvalidDeviceCertCredentials,
    ))?;

    let pem = PemCertificate::from(&device_identity_cert).context(ErrorKind::Initialize(
        InitializeErrorReason::InvalidDeviceCertCredentials,
    ))?;

    let hyper_client = MaybeProxyClient::new(get_proxy_uri(None)?, Some(pem), None)
        .context(ErrorKind::Initialize(InitializeErrorReason::HttpClient))?;

    let cert_data = IdentityCertificateData {
        common_name,
        thumbprint,
    };
    info!("Finished initializing hsm X509 interface...");

    Ok((hyper_client, Some(cert_data)))
}

fn get_thumbprint<T: Certificate>(id_cert: &T) -> Result<String, Error> {
    let cert_pem = id_cert
        .pem()
        .context(ErrorKind::Initialize(InitializeErrorReason::Hsm))?;
    Ok(format!("{:x}", Sha256::digest(cert_pem.as_bytes())))
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

fn prepare_master_hybrid_identity_key<S, C>(
    settings: &S,
    crypto: &C,
    subdir: &Path,
    hybrid_id_filename: &str,
    iv_filename: &str,
    provisioning_result: Option<&ProvisioningResult>,
) -> Result<(bool, Option<Vec<u8>>), Error>
where
    S: RuntimeSettings,
    C: CreateCertificate + Decrypt + Encrypt + MakeRandom,
{
    if get_provisioning_auth_method(settings, provisioning_result)? == ProvisioningAuthMethod::X509
    {
        let (new_key_created, hybrid_id_key) =
            get_or_create_hybrid_identity_key(crypto, subdir, hybrid_id_filename, iv_filename)?;
        Ok((new_key_created, Some(hybrid_id_key)))
    } else {
        // cleanup any stale keys from a prior run in case provisioning mode was changed
        // ignore errors from this operation because we could also be recovering from a previous bad
        // configuration and shouldn't stall the current configuration because of that
        let _u = fs::remove_dir_all(subdir);
        Ok((false, None))
    }
}

fn get_or_create_hybrid_identity_key<C>(
    crypto: &C,
    subdir: &Path,
    hybrid_id_filename: &str,
    iv_filename: &str,
) -> Result<(bool, Vec<u8>), Error>
where
    C: CreateCertificate + Decrypt + Encrypt + MakeRandom,
{
    fn get_hybrid_identity_key_inner<C>(
        crypto: &C,
        subdir: &Path,
        hybrid_id_filename: &str,
        iv_filename: &str,
    ) -> Result<Vec<u8>, Error>
    where
        C: Decrypt,
    {
        // check if the identity key & iv files exist and are valid
        let key_path = subdir.join(hybrid_id_filename);
        let enc_identity_key = fs::read(key_path).context(ErrorKind::Initialize(
            InitializeErrorReason::HybridAuthKeyLoad,
        ))?;
        let iv_path = subdir.join(iv_filename);
        let iv = fs::read(iv_path).context(ErrorKind::Initialize(
            InitializeErrorReason::HybridAuthKeyLoad,
        ))?;
        if iv.len() == IOTEDGED_CRYPTO_IV_LEN_BYTES {
            let identity_key = crypto
                .decrypt(
                    IOTEDGED_CRYPTO_ID.as_bytes(),
                    enc_identity_key.as_ref(),
                    &iv,
                )
                .context(ErrorKind::Initialize(
                    InitializeErrorReason::HybridAuthKeyInvalid,
                ))?;
            if identity_key.as_ref().len() == IDENTITY_MASTER_KEY_LEN_BYTES {
                Ok(identity_key.as_ref().to_vec())
            } else {
                Err(Error::from(ErrorKind::Initialize(
                    InitializeErrorReason::HybridAuthKeyInvalid,
                )))
            }
        } else {
            Err(Error::from(ErrorKind::Initialize(
                InitializeErrorReason::HybridAuthKeyInvalid,
            )))
        }
    }

    match get_hybrid_identity_key_inner(crypto, subdir, hybrid_id_filename, iv_filename) {
        Ok(hybrid_key) => Ok((false, hybrid_key)),
        Err(err) => {
            info!(
                "Error loading the hybrid identity key. Re-creating a new key. {}.",
                err
            );
            let key_bytes =
                create_hybrid_identity_key(crypto, subdir, hybrid_id_filename, iv_filename)?;
            Ok((true, key_bytes))
        }
    }
}

fn create_hybrid_identity_key<C>(
    crypto: &C,
    subdir: &Path,
    hybrid_id_filename: &str,
    iv_filename: &str,
) -> Result<Vec<u8>, Error>
where
    C: Decrypt + Encrypt + MakeRandom,
{
    // Ignore errors from this operation because we could be recovering from a previous bad
    // configuration and shouldn't stall the current configuration because of that
    let _u = fs::remove_dir_all(subdir);
    DirBuilder::new()
        .recursive(true)
        .create(subdir)
        .context(ErrorKind::Initialize(
            InitializeErrorReason::HybridAuthDirCreate,
        ))?;

    let mut key_bytes: [u8; IDENTITY_MASTER_KEY_LEN_BYTES] = [0; IDENTITY_MASTER_KEY_LEN_BYTES];
    crypto
        .get_random_bytes(&mut key_bytes)
        .context(ErrorKind::Initialize(
            InitializeErrorReason::HybridAuthKeyCreate,
        ))?;

    let mut iv: [u8; IOTEDGED_CRYPTO_IV_LEN_BYTES] = [0; IOTEDGED_CRYPTO_IV_LEN_BYTES];
    crypto
        .get_random_bytes(&mut iv)
        .context(ErrorKind::Initialize(
            InitializeErrorReason::HybridAuthKeyCreate,
        ))?;

    let enc_identity_key = crypto
        .encrypt(IOTEDGED_CRYPTO_ID.as_bytes(), &key_bytes, &iv)
        .context(ErrorKind::Initialize(
            InitializeErrorReason::HybridAuthKeyCreate,
        ))?;

    let path = subdir.join(hybrid_id_filename);
    let mut file = File::create(path).context(ErrorKind::Initialize(
        InitializeErrorReason::HybridAuthKeyCreate,
    ))?;
    file.write_all(enc_identity_key.as_bytes())
        .context(ErrorKind::Initialize(
            InitializeErrorReason::HybridAuthKeyCreate,
        ))?;

    let path = subdir.join(iv_filename);
    let mut file = File::create(path).context(ErrorKind::Initialize(
        InitializeErrorReason::HybridAuthKeyCreate,
    ))?;
    file.write_all(&iv).context(ErrorKind::Initialize(
        InitializeErrorReason::HybridAuthKeyCreate,
    ))?;

    Ok(key_bytes.to_vec())
}

fn compute_settings_digest<S>(
    settings: &S,
    id_cert_thumbprint: Option<&str>,
) -> Result<String, DiffError>
where
    S: RuntimeSettings + Serialize,
{
    let mut s = serde_json::to_string(settings)?;
    if let Some(thumbprint) = id_cert_thumbprint {
        s.push_str(thumbprint);
    }
    Ok(base64::encode(&Sha256::digest_str(&s)))
}

fn diff_with_cached<S>(settings: &S, path: &Path, id_cert_thumbprint: Option<&str>) -> bool
where
    S: RuntimeSettings + Serialize,
{
    fn diff_with_cached_inner<S>(
        cached_settings: &S,
        path: &Path,
        id_cert_thumbprint: Option<&str>,
    ) -> Result<bool, DiffError>
    where
        S: RuntimeSettings + Serialize,
    {
        let mut file = OpenOptions::new().read(true).open(path)?;
        let mut buffer = String::new();
        file.read_to_string(&mut buffer)?;
        let encoded = compute_settings_digest(cached_settings, id_cert_thumbprint)?;
        if encoded == buffer {
            debug!("Config state matches supplied config.");
            Ok(false)
        } else {
            Ok(true)
        }
    }

    match diff_with_cached_inner(settings, path, id_cert_thumbprint) {
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
    subdir: &Path,
    filename: &str,
    settings: &M::Settings,
    runtime: &M::ModuleRuntime,
    crypto: &C,
    tokio_runtime: &mut tokio::runtime::Runtime,
    id_cert_thumbprint: Option<&str>,
) -> Result<(), Error>
where
    M: MakeModuleRuntime + 'static,
    M::Settings: Serialize,
    C: CreateCertificate + GetIssuerAlias + MasterEncryptionKey,
{
    info!("Detecting if configuration file has changed...");
    let path = subdir.join(filename);
    let mut reconfig_reqd = false;
    let diff = diff_with_cached(settings, &path, id_cert_thumbprint);
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
            subdir,
            filename,
            settings,
            runtime,
            crypto,
            tokio_runtime,
            id_cert_thumbprint,
        )?;
    }
    Ok(())
}

fn get_provisioning_auth_method<S>(
    settings: &S,
    provisioning_result: Option<&ProvisioningResult>,
) -> Result<ProvisioningAuthMethod, Error>
where
    S: RuntimeSettings,
{
    match settings.provisioning().provisioning_type() {
        ProvisioningType::Manual(manual) => {
            if let ManualAuthMethod::X509(_) = manual.authentication_method() {
                return Ok(ProvisioningAuthMethod::X509);
            }
        }
        ProvisioningType::External(_external) => {
            let prov_result = provisioning_result.as_ref().ok_or_else(|| {
                ErrorKind::Initialize(InitializeErrorReason::ExternalProvisioningClient(
                    ExternalProvisioningErrorReason::Provisioning,
                ))
            })?;

            if let Some(credentials) = prov_result.credentials() {
                if let AuthType::X509(_x509) = credentials.auth_type() {
                    return Ok(ProvisioningAuthMethod::X509);
                }
            }
        }
        ProvisioningType::Dps(dps) => {
            if let AttestationMethod::X509(_) = dps.attestation() {
                return Ok(ProvisioningAuthMethod::X509);
            }
        }
    }
    Ok(ProvisioningAuthMethod::SharedAccessKey)
}

fn reconfigure<M, C>(
    subdir: &Path,
    filename: &str,
    settings: &M::Settings,
    runtime: &M::ModuleRuntime,
    crypto: &C,
    tokio_runtime: &mut tokio::runtime::Runtime,
    id_cert_thumbprint: Option<&str>,
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
    let _u = fs::remove_dir_all(subdir);

    let path = subdir.join(filename);

    DirBuilder::new()
        .recursive(true)
        .create(subdir)
        .context(ErrorKind::Initialize(
            InitializeErrorReason::CreateSettingsDirectory,
        ))?;

    // regenerate the workload CA certificate
    destroy_workload_ca(crypto)?;
    prepare_workload_ca(crypto)?;
    let mut file =
        File::create(path).context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;
    let digest = compute_settings_digest(settings, id_cert_thumbprint)
        .context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;
    file.write_all(digest.as_bytes())
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
) -> Result<(StartApiReturnStatus, bool), Error>
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
    let (mgmt_stop_and_reprovision_tx, mgmt_stop_and_reprovision_rx) = mpsc::unbounded();
    let (work_tx, work_rx) = oneshot::channel();

    let edgelet_cert_props = CertificateProperties::new(
        settings.certificates().auto_generated_ca_lifetime_seconds(),
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

    let mgmt = start_management::<_, _, _, M>(
        settings,
        runtime,
        &id_man,
        mgmt_rx,
        cert_manager.clone(),
        mgmt_stop_and_reprovision_tx,
    );

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

    // This mpsc sender/receiver is used for getting notifications from the mgmt service
    // indicating that the daemon should shut down and attempt to reprovision the device.
    let mgmt_stop_and_reprovision_signaled = mgmt_stop_and_reprovision_rx
        .then(|res| match res {
            Ok(_) => Err(None),
            Err(_) => Err(Some(Error::from(ErrorKind::ManagementService))),
        })
        .for_each(move |_x: Option<Error>| Ok(()))
        .then(|res| match res {
            Ok(_) | Err(None) => Ok(None),
            Err(Some(e)) => Err(Some(e)),
        });

    let mgmt_stop_and_reprovision_signaled = if settings.provisioning().dynamic_reprovisioning() {
        futures::future::Either::B(mgmt_stop_and_reprovision_signaled)
    } else {
        futures::future::Either::A(future::empty())
    };

    let edge_rt_with_mgmt_signal = edge_rt.select2(mgmt_stop_and_reprovision_signaled).then(
        |res: Result<
            Either<((), _), (Option<Error>, _)>,
            Either<(Error, _), (Option<Error>, _)>,
        >| {
            // A -> EdgeRt Future
            // B -> Mgmt Stop and Reprovision Signal Future
            match res {
                Ok(Either::A((_x, _y))) => {
                    Ok((StartApiReturnStatus::Shutdown, false)).into_future()
                }
                Ok(Either::B((_x, _y))) => {
                    debug!("Shutdown with device reprovisioning.");
                    Ok((StartApiReturnStatus::Shutdown, true)).into_future()
                }
                Err(Either::A((err, _y))) => Err(err).into_future(),
                Err(Either::B((err, _y))) => {
                    debug!("The mgmt shutdown and reprovision signal failed.");
                    Err(err.unwrap()).into_future()
                }
            }
        },
    );

    // Wait for the watchdog to finish, and then send signal to the workload and management services.
    // This way the edgeAgent can finish shutting down all modules.
    let edge_rt_with_cleanup = edge_rt_with_mgmt_signal
        .select2(restart_rx)
        .then(move |res| {
            mgmt_tx.send(()).unwrap_or(());
            work_tx.send(()).unwrap_or(());

            // A -> EdgeRt + Mgmt Stop and Reprovision Signal Future
            // B -> Restart Signal Future
            match res {
                Ok(Either::A((x, _))) => Ok((StartApiReturnStatus::Shutdown, x.1)).into_future(),
                Ok(Either::B(_)) => Ok((StartApiReturnStatus::Restart, false)).into_future(),
                Err(Either::A((err, _))) => Err(err).into_future(),
                Err(Either::B(_)) => {
                    debug!("The restart signal failed, shutting down.");
                    Ok((StartApiReturnStatus::Shutdown, false)).into_future()
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
            Ok(((), (), (code, should_reprovision), ())) => Ok((code, should_reprovision)),
            Err(err) => Err(err),
        });
    let (restart_code, should_reprovision) = tokio_runtime.block_on(services)?;
    Ok((restart_code, should_reprovision))
}

fn init_runtime<M>(
    settings: M::Settings,
    tokio_runtime: &mut tokio::runtime::Runtime,
    provisioning_result: M::ProvisioningResult,
    crypto: Crypto,
) -> Result<M::ModuleRuntime, Error>
where
    M: MakeModuleRuntime + Send + 'static,
    M::ModuleRuntime: Send,
    M::Future: 'static,
{
    info!("Initializing the module runtime...");
    let runtime = tokio_runtime
        .block_on(M::make_runtime(settings, provisioning_result, crypto))
        .context(ErrorKind::Initialize(InitializeErrorReason::ModuleRuntime))?;
    info!("Finished initializing the module runtime.");

    Ok(runtime)
}

fn manual_provision_connection_string(
    manual: &ManualProvisioning,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<(DerivedKeyStore<MemoryKey>, ProvisioningResult, MemoryKey), Error> {
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

fn manual_provision_x509(
    manual: &ManualProvisioning,
    tokio_runtime: &mut tokio::runtime::Runtime,
    cert_thumbprint: String,
) -> Result<(DerivedKeyStore<MemoryKey>, ProvisioningResult, MemoryKey), Error> {
    let memory_hsm = MemoryKeyStore::new();
    let provision = manual
        .provision(memory_hsm.clone())
        .map_err(|err| {
            Error::from(err.context(ErrorKind::Initialize(
                InitializeErrorReason::ManualProvisioningClient,
            )))
        })
        .and_then(move |prov_result| {
            let (derived_key_store, hybrid_derived_key) = prepare_derived_hybrid_key(
                &memory_hsm,
                &cert_thumbprint,
                prov_result.hub_name(),
                prov_result.device_id(),
            )?;
            Ok((derived_key_store, prov_result, hybrid_derived_key))
        });
    tokio_runtime.block_on(provision)
}

fn dps_x509_provision_init<HC>(
    dps: &Dps,
    hyper_client: HC,
    x509_info: &X509AttestationInfo,
    hybrid_identity_key: Option<Vec<u8>>,
    common_name: &str,
) -> Result<(MemoryKeyStore, DpsX509Provisioning<HC>), Error>
where
    HC: 'static + ClientImpl,
{
    // use the client provided registration id if provided else use the CN
    let reg_id = match x509_info.registration_id() {
        Some(id) => id.to_string(),
        None => common_name.to_string(),
    };

    let key_bytes = hybrid_identity_key
        .ok_or_else(|| ErrorKind::Initialize(InitializeErrorReason::DpsProvisioningClient))?;

    let mut memory_hsm = MemoryKeyStore::new();
    memory_hsm
        .activate_identity_key(KeyIdentity::Device, "primary".to_string(), key_bytes)
        .context(ErrorKind::ActivateSymmetricKey)?;
    let dps_x509 = DpsX509Provisioning::new(
        hyper_client,
        dps.global_endpoint().clone(),
        dps.scope_id().to_string(),
        reg_id,
        DPS_API_VERSION.to_string(),
    )
    .context(ErrorKind::Initialize(
        InitializeErrorReason::DpsProvisioningClient,
    ))?;

    Ok((memory_hsm, dps_x509))
}

#[allow(clippy::too_many_arguments)]
fn dps_x509_provision<HC>(
    memory_hsm: MemoryKeyStore,
    dps: &DpsX509Provisioning<HC>,
    backup_path: PathBuf,
    tokio_runtime: &mut tokio::runtime::Runtime,
    cert_thumbprint: String,
) -> Result<(DerivedKeyStore<MemoryKey>, ProvisioningResult, MemoryKey), Error>
where
    HC: 'static + ClientImpl,
{
    let provision_with_file_backup = BackupProvisioning::new(dps, backup_path);

    let provision = provision_with_file_backup
        .provision(memory_hsm.clone())
        .map_err(|err| {
            Error::from(err.context(ErrorKind::Initialize(
                InitializeErrorReason::DpsProvisioningClient,
            )))
        })
        .and_then(move |prov_result| {
            info!("Successful DPS provisioning.");
            let (derived_key_store, hybrid_derived_key) = prepare_derived_hybrid_key(
                &memory_hsm,
                &cert_thumbprint,
                prov_result.hub_name(),
                prov_result.device_id(),
            )?;
            Ok((derived_key_store, prov_result, hybrid_derived_key))
        });
    tokio_runtime.block_on(provision)
}

fn prepare_derived_hybrid_key(
    key_store: &MemoryKeyStore,
    cert_thumbprint: &str,
    hub_name: &str,
    device_id: &str,
) -> Result<(DerivedKeyStore<MemoryKey>, MemoryKey), Error> {
    let k = key_store
        .get(&KeyIdentity::Device, "primary")
        .context(ErrorKind::Initialize(
            InitializeErrorReason::HybridAuthKeyGet,
        ))?;
    let sign_data = format!("{}/devices/{}/{}", hub_name, device_id, cert_thumbprint);
    let digest = k
        .sign(SignatureAlgorithm::HMACSHA256, sign_data.as_bytes())
        .context(ErrorKind::Initialize(
            InitializeErrorReason::HybridAuthKeySign,
        ))?;
    let hybrid_derived_key = MemoryKey::new(digest.as_bytes());
    let derived_key_store = DerivedKeyStore::new(hybrid_derived_key.clone());
    Ok((derived_key_store, hybrid_derived_key))
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

fn external_provision_x509(
    provisioning_result: &ProvisioningResult,
    hybrid_identity_key: &[u8],
    cert_thumbprint: &str,
) -> Result<(DerivedKeyStore<MemoryKey>, MemoryKey), Error> {
    let memory_key = MemoryKey::new(hybrid_identity_key);
    let mut memory_hsm = MemoryKeyStore::new();
    memory_hsm.insert(&KeyIdentity::Device, "primary", memory_key);

    let (derived_key_store, hybrid_derived_key) = prepare_derived_hybrid_key(
        &memory_hsm,
        cert_thumbprint,
        provisioning_result.hub_name(),
        provisioning_result.device_id(),
    )
    .context(ErrorKind::Initialize(
        InitializeErrorReason::ExternalProvisioningClient(
            ExternalProvisioningErrorReason::HybridKeyPreparation,
        ),
    ))?;

    Ok((derived_key_store, hybrid_derived_key))
}

fn dps_symmetric_key_provision_init<HC>(
    provisioning: &Dps,
    hyper_client: HC,
    key: &SymmetricKeyAttestationInfo,
) -> Result<(MemoryKeyStore, DpsSymmetricKeyProvisioning<HC>), Error>
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
    Ok((memory_hsm, dps))
}

fn dps_symmetric_key_provision<HC>(
    backup_path: PathBuf,
    tokio_runtime: &mut tokio::runtime::Runtime,
    memory_hsm: MemoryKeyStore,
    dps: &DpsSymmetricKeyProvisioning<HC>,
) -> Result<(DerivedKeyStore<MemoryKey>, ProvisioningResult, MemoryKey), Error>
where
    HC: 'static + ClientImpl,
{
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

fn dps_tpm_provision_init<HC>(
    provisioning: &Dps,
    hyper_client: HC,
    tpm_attestation_info: &TpmAttestationInfo,
) -> Result<(Tpm, DpsTpmProvisioning<HC>), Error>
where
    HC: 'static + ClientImpl,
{
    let tpm = Tpm::new().context(ErrorKind::Initialize(
        InitializeErrorReason::DpsProvisioningClient,
    ))?;

    let hsm_version = tpm
        .get_version()
        .context(ErrorKind::Initialize(InitializeErrorReason::Hsm))?;

    if hsm_version != IOTEDGE_COMPAT_HSM_VERSION {
        info!(
            "Incompatible HSM interface version for TPM. Found {}, required {}",
            hsm_version, IOTEDGE_COMPAT_HSM_VERSION
        );
        return Err(Error::from(ErrorKind::Initialize(
            InitializeErrorReason::IncompatibleHsmVersion,
        )));
    }

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
    Ok((tpm, dps))
}

fn dps_tpm_provision<HC>(
    backup_path: PathBuf,
    tokio_runtime: &mut tokio::runtime::Runtime,
    hsm_lock: Arc<HsmLock>,
    tpm: Tpm,
    dps: &DpsTpmProvisioning<HC>,
) -> Result<(DerivedKeyStore<TpmKey>, ProvisioningResult, TpmKey), Error>
where
    HC: 'static + ClientImpl,
{
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

    let watchdog = Watchdog::new(runtime, id_man.clone(), settings.watchdog().max_retries());
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

    #[cfg(feature = "runtime-docker")]
    let (workload_uri, management_uri) = (
        settings.connect().workload_uri().to_string(),
        settings.connect().management_uri().to_string(),
    );
    #[cfg(feature = "runtime-kubernetes")]
    let (workload_uri, management_uri) = (
        format!(
            "http://localhost:{}",
            settings.connect().workload_uri().port().unwrap_or(80u16)
        ),
        format!(
            "http://localhost:{}",
            settings.connect().management_uri().port().unwrap_or(80u16)
        ),
    );

    env.insert(WORKLOAD_URI_KEY.to_string(), workload_uri);
    env.insert(MANAGEMENT_URI_KEY.to_string(), management_uri);
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
    initiate_shutdown_and_reprovision: mpsc::UnboundedSender<()>,
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
    let min_protocol_version = settings.listen().min_tls_version();

    ManagementService::new(runtime, id_man, initiate_shutdown_and_reprovision)
        .then(move |service| -> Result<_, Error> {
            let service = service.context(ErrorKind::Initialize(
                InitializeErrorReason::ManagementService,
            ))?;
            let service = LoggingService::new(label, service);

            let tls_params = TlsAcceptorParams::new(&cert_manager, min_protocol_version);

            let run = Http::new()
                .bind_url(url.clone(), service, Some(tls_params))
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
    let min_protocol_version = settings.listen().min_tls_version();

    WorkloadService::new(key_store, crypto.clone(), runtime, config)
        .then(move |service| -> Result<_, Error> {
            let service = service.context(ErrorKind::Initialize(
                InitializeErrorReason::WorkloadService,
            ))?;
            let service = LoggingService::new(label, service);

            let tls_params = TlsAcceptorParams::new(&cert_manager, min_protocol_version);

            let run = Http::new()
                .bind_url(url.clone(), service, Some(tls_params))
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
    use std::sync::Mutex;

    use chrono::{Duration, Utc};
    use lazy_static::lazy_static;
    use rand::RngCore;
    use serde_json::json;
    use tempdir::TempDir;

    use edgelet_core::{
        KeyBytes, ModuleRuntimeState, PrivateKey, DEFAULT_AUTO_GENERATED_CA_LIFETIME_DAYS,
    };
    use edgelet_docker::{DockerConfig, DockerModuleRuntime, Settings};
    use edgelet_test_utils::cert::TestCert;
    use edgelet_test_utils::crypto::TestHsm;
    use edgelet_test_utils::module::*;

    use provisioning::provisioning::{
        AuthType, CredentialSource, Credentials, ProvisioningResult, ReprovisioningStatus,
        SymmetricKeyCredential, X509Credential,
    };

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
    static GOOD_SETTINGS_DPS_SYMM_KEY: &str = "test/linux/sample_settings.dps.symm.key.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_DPS_DEFAULT: &str =
        "../edgelet-docker/test/linux/sample_settings.dps.default.yaml";
    #[cfg(unix)]
    static EMPTY_CONNECTION_STRING_SETTINGS: &str =
        "../edgelet-docker/test/linux/bad_sample_settings.cs.3.yaml";
    #[cfg(unix)]
    static DEFAULT_CONNECTION_STRING_SETTINGS: &str =
        "../edgelet-docker/test/linux/bad_sample_settings.cs.4.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_EXTERNAL: &str =
        "../edgelet-docker/test/linux/sample_settings.external.1.yaml";

    #[cfg(windows)]
    static GOOD_SETTINGS: &str = "../edgelet-docker/test/windows/sample_settings.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS1: &str = "test/windows/sample_settings1.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS2: &str = "test/windows/sample_settings2.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_DPS_TPM1: &str = "test/windows/sample_settings.dps.tpm.1.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_DPS_SYMM_KEY: &str = "test/windows/sample_settings.dps.symm.key.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_DPS_DEFAULT: &str =
        "../edgelet-docker/test/windows/sample_settings.dps.default.yaml";
    #[cfg(windows)]
    static EMPTY_CONNECTION_STRING_SETTINGS: &str =
        "../edgelet-docker/test/windows/bad_sample_settings.cs.3.yaml";
    #[cfg(windows)]
    static DEFAULT_CONNECTION_STRING_SETTINGS: &str =
        "../edgelet-docker/test/windows/bad_sample_settings.cs.4.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_EXTERNAL: &str =
        "../edgelet-docker/test/windows/sample_settings.external.1.yaml";

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
        fail_decrypt: bool,
        fail_encrypt: bool,
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

    impl MakeRandom for TestCrypto {
        fn get_random_bytes(&self, buffer: &mut [u8]) -> Result<(), edgelet_core::Error> {
            rand::thread_rng().fill_bytes(buffer);
            Ok(())
        }
    }

    impl Encrypt for TestCrypto {
        type Buffer = Vec<u8>;

        fn encrypt(
            &self,
            _client_id: &[u8],
            plaintext: &[u8],
            _initialization_vector: &[u8],
        ) -> Result<Self::Buffer, edgelet_core::Error> {
            // pass thru plaintext or error
            if self.fail_encrypt {
                Err(edgelet_core::Error::from(edgelet_core::ErrorKind::KeyStore))
            } else {
                Ok(Vec::from(plaintext))
            }
        }
    }

    impl Decrypt for TestCrypto {
        // type Buffer = Buffer;
        type Buffer = Vec<u8>;

        fn decrypt(
            &self,
            _client_id: &[u8],
            ciphertext: &[u8],
            _initialization_vector: &[u8],
        ) -> Result<Self::Buffer, edgelet_core::Error> {
            // pass thru ciphertext or error
            if self.fail_decrypt {
                Err(edgelet_core::Error::from(edgelet_core::ErrorKind::KeyStore))
            } else {
                Ok(Vec::from(ciphertext))
            }
        }
    }

    lazy_static! {
        // Tests that call Main::new cannot run in parallel because they initialize hsm-sys
        // (via hsm_client_crypto_init) which is not thread-safe.
        static ref LOCK: Mutex<()> = Mutex::new(());
    }

    #[test]
    fn default_settings_raise_load_error() {
        let _guard = LOCK.lock().unwrap();

        let settings = Settings::new(Path::new(DEFAULT_CONNECTION_STRING_SETTINGS)).unwrap();
        let main = Main::<DockerModuleRuntime>::new(settings);
        let result = main.run_until(signal::shutdown);
        match result.unwrap_err().kind() {
            ErrorKind::Initialize(InitializeErrorReason::LoadSettings) => (),
            kind => panic!("Expected `LoadSettings` but got {:?}", kind),
        }
    }

    #[test]
    fn empty_connection_string_raises_load_error() {
        let _guard = LOCK.lock().unwrap();

        let settings = Settings::new(Path::new(EMPTY_CONNECTION_STRING_SETTINGS)).unwrap();
        let main = Main::<DockerModuleRuntime>::new(settings);
        let result = main.run_until(signal::shutdown);
        match result.unwrap_err().kind() {
            ErrorKind::Initialize(InitializeErrorReason::LoadSettings) => (),
            kind => panic!("Expected `LoadSettings` but got {:?}", kind),
        }
    }

    #[test]
    fn settings_without_cert_life_uses_default() {
        let _guard = LOCK.lock().unwrap();

        let settings = Settings::new(Path::new(GOOD_SETTINGS1)).unwrap();
        assert_eq!(
            u64::from(DEFAULT_AUTO_GENERATED_CA_LIFETIME_DAYS) * 86_400,
            settings.certificates().auto_generated_ca_lifetime_seconds()
        );
    }

    #[test]
    fn settings_with_cert_life_uses_value() {
        let _guard = LOCK.lock().unwrap();

        let settings = Settings::new(Path::new(GOOD_SETTINGS2)).unwrap();
        // Provided value is 1 day so check for that in seconds
        assert_eq!(
            86_400,
            settings.certificates().auto_generated_ca_lifetime_seconds()
        );
    }

    #[test]
    fn settings_with_invalid_issuer_ca_fails() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::new(Path::new(GOOD_SETTINGS)).unwrap();
        let config = DockerConfig::new(
            "microsoft/test-image".to_string(),
            ContainerCreateBody::new(),
            None,
        )
        .unwrap();
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error, _> =
            TestModule::new_with_config("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::make_runtime(
            settings.clone(),
            TestProvisioningResult::new(),
            TestHsm::default(),
        )
        .wait()
        .unwrap()
        .with_module(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: true,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        let result = check_settings_state::<TestRuntime<_, Settings>, _>(
            tmp_dir.path(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            None,
        );
        match result.unwrap_err().kind() {
            ErrorKind::Initialize(InitializeErrorReason::PrepareWorkloadCa) => (),
            kind => panic!("Expected `PrepareWorkloadCa` but got {:?}", kind),
        }
    }

    #[test]
    fn settings_with_expired_issuer_ca_fails() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::new(Path::new(GOOD_SETTINGS)).unwrap();
        let config = DockerConfig::new(
            "microsoft/test-image".to_string(),
            ContainerCreateBody::new(),
            None,
        )
        .unwrap();
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error, _> =
            TestModule::new_with_config("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::make_runtime(
            settings.clone(),
            TestProvisioningResult::new(),
            TestHsm::default(),
        )
        .wait()
        .unwrap()
        .with_module(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: true,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        let result = check_settings_state::<TestRuntime<_, Settings>, _>(
            tmp_dir.path(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            None,
        );
        match result.unwrap_err().kind() {
            ErrorKind::Initialize(InitializeErrorReason::IssuerCAExpiration) => (),
            kind => panic!("Expected `IssuerCAExpiration` but got {:?}", kind),
        }
    }

    fn settings_first_time_creates_backup(settings_path: &str) {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::new(Path::new(settings_path)).unwrap();
        let config = DockerConfig::new(
            "microsoft/test-image".to_string(),
            ContainerCreateBody::new(),
            None,
        )
        .unwrap();
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error, _> =
            TestModule::new_with_config("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::make_runtime(
            settings.clone(),
            TestProvisioningResult::new(),
            TestHsm::default(),
        )
        .wait()
        .unwrap()
        .with_module(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        check_settings_state::<TestRuntime<_, Settings>, _>(
            tmp_dir.path(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            None,
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

        // non x.509 auth modes shouldn't have these files created
        assert!(!tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME)
            .exists());
        assert!(!tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME)
            .exists());
    }

    #[test]
    fn settings_manual_connection_string_auth_first_time_creates_backup() {
        settings_first_time_creates_backup(GOOD_SETTINGS);
    }

    #[test]
    fn settings_dps_symm_key_auth_first_time_creates_backup() {
        settings_first_time_creates_backup(GOOD_SETTINGS_DPS_SYMM_KEY);
    }

    #[test]
    fn settings_dps_tpm_auth_first_time_creates_backup() {
        settings_first_time_creates_backup(GOOD_SETTINGS_DPS_TPM1);
    }

    #[test]
    fn settings_external_provisioning_first_time_creates_backup() {
        settings_first_time_creates_backup(GOOD_SETTINGS_EXTERNAL);
    }

    #[test]
    fn settings_change_creates_new_backup() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::new(Path::new(GOOD_SETTINGS)).unwrap();
        let config = DockerConfig::new(
            "microsoft/test-image".to_string(),
            ContainerCreateBody::new(),
            None,
        )
        .unwrap();
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error, _> =
            TestModule::new_with_config("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::make_runtime(
            settings.clone(),
            TestProvisioningResult::new(),
            TestHsm::default(),
        )
        .wait()
        .unwrap()
        .with_module(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: true,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        check_settings_state::<TestRuntime<_, Settings>, _>(
            tmp_dir.path(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            None,
        )
        .unwrap();
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();

        let settings1 = Settings::new(Path::new(GOOD_SETTINGS1)).unwrap();
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        check_settings_state::<TestRuntime<_, Settings>, _>(
            tmp_dir.path(),
            "settings_state",
            &settings1,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            None,
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
        let settings = Settings::new(Path::new(GOOD_SETTINGS)).unwrap();
        let settings_to_write = serde_json::to_string(&settings).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        File::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        assert!(!diff_with_cached(&settings, &path, None));
    }

    #[test]
    fn diff_with_tpm_default_and_explicit_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 = Settings::new(Path::new(GOOD_SETTINGS_DPS_DEFAULT)).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        File::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings = Settings::new(Path::new(GOOD_SETTINGS_DPS_TPM1)).unwrap();
        assert!(!diff_with_cached(&settings, &path, None));
    }

    #[test]
    fn diff_with_tpm_explicit_and_default_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 = Settings::new(Path::new(GOOD_SETTINGS_DPS_TPM1)).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        File::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings = Settings::new(Path::new(GOOD_SETTINGS_DPS_DEFAULT)).unwrap();
        assert!(!diff_with_cached(&settings, &path, None));
    }

    #[test]
    fn diff_with_same_cached_env_var_unordered_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 = Settings::new(Path::new(GOOD_SETTINGS2)).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        File::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings = Settings::new(Path::new(GOOD_SETTINGS)).unwrap();
        assert!(!diff_with_cached(&settings, &path, None));
    }

    #[test]
    fn diff_with_different_cached_returns_true() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 = Settings::new(Path::new(GOOD_SETTINGS1)).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        File::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings = Settings::new(Path::new(GOOD_SETTINGS)).unwrap();
        assert!(diff_with_cached(&settings, &path, None));
    }

    #[test]
    fn diff_with_no_file_returns_true() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS)).unwrap();
        assert!(diff_with_cached(&settings, Path::new("i dont exist"), None));
    }

    #[test]
    fn get_provisioning_auth_method_returns_sas_key_for_manual_connection_string() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS1)).unwrap();
        assert_eq!(
            ProvisioningAuthMethod::SharedAccessKey,
            get_provisioning_auth_method(&settings, None).unwrap()
        );
    }

    #[test]
    fn get_provisioning_auth_method_returns_saskey_for_dps_tpm_provisioning() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_DPS_TPM1)).unwrap();
        assert_eq!(
            ProvisioningAuthMethod::SharedAccessKey,
            get_provisioning_auth_method(&settings, None).unwrap()
        );
    }

    #[test]
    fn get_provisioning_auth_method_returns_saskey_for_dps_symm_key_provisioning() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_DPS_SYMM_KEY)).unwrap();
        assert_eq!(
            ProvisioningAuthMethod::SharedAccessKey,
            get_provisioning_auth_method(&settings, None).unwrap()
        );
    }

    #[test]
    fn get_provisioning_auth_method_returns_x509_for_external_provisioning_with_x509_auth_type() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_EXTERNAL)).unwrap();

        let x509_credential = X509Credential::new("".to_string(), "".to_string());
        let credentials =
            Credentials::new(AuthType::X509(x509_credential), CredentialSource::Payload);

        let hub_name = "TestHub";
        let device_id = "TestDevice";

        let provisioning_result = Some(ProvisioningResult::new(
            device_id,
            hub_name,
            None,
            ReprovisioningStatus::InitialAssignment,
            Some(credentials),
        ));
        assert_eq!(
            ProvisioningAuthMethod::X509,
            get_provisioning_auth_method(&settings, provisioning_result.as_ref()).unwrap()
        );
    }

    #[test]
    fn get_provisioning_auth_method_returns_sas_key_for_external_provisioning_with_sas_key_auth_type(
    ) {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_EXTERNAL)).unwrap();

        let symmetric_key_credential = SymmetricKeyCredential::new(vec![0_u8; 10]);
        let credentials = Credentials::new(
            AuthType::SymmetricKey(symmetric_key_credential),
            CredentialSource::Payload,
        );

        let hub_name = "TestHub";
        let device_id = "TestDevice";

        let provisioning_result = Some(ProvisioningResult::new(
            device_id,
            hub_name,
            None,
            ReprovisioningStatus::InitialAssignment,
            Some(credentials),
        ));
        assert_eq!(
            ProvisioningAuthMethod::SharedAccessKey,
            get_provisioning_auth_method(&settings, provisioning_result.as_ref()).unwrap()
        );
    }

    #[test]
    fn get_provisioning_auth_method_returns_error_with_no_provisioning_result_in_external_provisioning(
    ) {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_EXTERNAL)).unwrap();

        assert_eq!(
            &ErrorKind::Initialize(InitializeErrorReason::ExternalProvisioningClient(
                ExternalProvisioningErrorReason::Provisioning,
            )),
            get_provisioning_auth_method(&settings, None).expect_err("An error is expected when no provisioning result is specified with the external provisioning mode.").kind()
        );
    }

    fn prepare_test_dps_x509_settings_yaml(
        settings_path: &Path,
        cert_path: &Path,
        key_path: &Path,
    ) -> String {
        File::create(&cert_path)
            .expect("Test cert file could not be created")
            .write_all(b"CN=Mr. T")
            .expect("Test cert file could not be written");

        File::create(&key_path)
            .expect("Test cert private key file could not be created")
            .write_all(b"i pity the fool")
            .expect("Test cert private key file could not be written");

        let cert_uri = format!(
            "file://{}",
            cert_path.canonicalize().unwrap().to_str().unwrap()
        );
        let pk_uri = format!(
            "file://{}",
            key_path.canonicalize().unwrap().to_str().unwrap()
        );
        let settings_yaml = json!({
        "provisioning": {
            "source": "dps",
            "global_endpoint": "scheme://jibba-jabba.net",
            "scope_id": "i got no time for the jibba-jabba",
            "attestation": {
                "method": "x509",
                "identity_cert": cert_uri,
                "identity_pk": pk_uri,
            },
        }})
        .to_string();
        File::create(&settings_path)
            .expect("Test settings file could not be created")
            .write_all(settings_yaml.as_bytes())
            .expect("Test settings file could not be written");

        settings_yaml
    }

    #[test]
    fn get_provisioning_auth_method_returns_x509_for_dps_x509_provisioning() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let cert_path = tmp_dir.path().join("test_cert");
        let key_path = tmp_dir.path().join("test_key");
        let settings_path = tmp_dir.path().join("test_settings.yaml");

        prepare_test_dps_x509_settings_yaml(&settings_path, &cert_path, &key_path);
        let settings = Settings::new(&settings_path).unwrap();
        assert_eq!(
            ProvisioningAuthMethod::X509,
            get_provisioning_auth_method(&settings, None).unwrap()
        );
    }

    #[test]
    fn dps_x509_auth_diff_also_checks_cert_thumbprint() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let cert_path = tmp_dir.path().join("test_cert");
        let key_path = tmp_dir.path().join("test_key");
        let settings_path = tmp_dir.path().join("test_settings.yaml");

        prepare_test_dps_x509_settings_yaml(&settings_path, &cert_path, &key_path);
        let settings = Settings::new(&settings_path).unwrap();

        let path = tmp_dir.path().join("cache");
        let base64_to_write = compute_settings_digest(&settings, Some("thumbprint-1")).unwrap();
        File::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();

        // check if there is no diff
        assert_eq!(
            diff_with_cached(&settings, &path, Some("thumbprint-1")),
            false
        );

        // now modify only the cert thumbprint and test if there is a diff
        assert_eq!(
            diff_with_cached(&settings, &path, Some("thumbprint-2")),
            true
        );
    }

    #[test]
    fn master_hybrid_id_key_create_first_time_creates_new_key_and_forces_reprovision() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let cert_path = tmp_dir.path().join("test_cert");
        let key_path = tmp_dir.path().join("test_key");
        let settings_path = tmp_dir.path().join("test_settings.yaml");

        prepare_test_dps_x509_settings_yaml(&settings_path, &cert_path, &key_path);
        let settings = Settings::new(&settings_path).unwrap();

        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let (force_module_reprovision, hybrid_identity_key) = prepare_master_hybrid_identity_key(
            &settings,
            &crypto,
            tmp_dir.path(),
            EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME,
            EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME,
            None,
        )
        .unwrap();

        // validate that module reprovision is required since this is the first time it was checked
        assert!(force_module_reprovision);
        assert_eq!(
            hybrid_identity_key.unwrap().len(),
            IDENTITY_MASTER_KEY_LEN_BYTES
        );

        // hybrid key and iv should be created and non empty since the auth mode is X.509
        assert!(
            tmp_dir
                .path()
                .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME)
                .metadata()
                .unwrap()
                .len()
                > 0
        );
        assert!(
            tmp_dir
                .path()
                .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME)
                .metadata()
                .unwrap()
                .len()
                == IOTEDGED_CRYPTO_IV_LEN_BYTES as u64
        );
    }

    #[test]
    fn master_hybrid_id_key_subsequent_checks_does_not_create_new_key_and_no_reprovision() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let cert_path = tmp_dir.path().join("test_cert");
        let key_path = tmp_dir.path().join("test_key");
        let settings_path = tmp_dir.path().join("test_settings.yaml");

        prepare_test_dps_x509_settings_yaml(&settings_path, &cert_path, &key_path);
        let settings = Settings::new(&settings_path).unwrap();

        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let (force_module_reprovision, expected_hybrid_identity_key) =
            prepare_master_hybrid_identity_key(
                &settings,
                &crypto,
                tmp_dir.path(),
                EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME,
                EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME,
                None,
            )
            .unwrap();

        // validate that module reprovision is required since this is the first time it was checked
        assert!(force_module_reprovision);
        assert_eq!(
            expected_hybrid_identity_key.clone().unwrap().len(),
            IDENTITY_MASTER_KEY_LEN_BYTES
        );

        // hybrid key and iv should be created and non empty since the auth mode is X.509
        let key_path = tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME);
        assert!(key_path.metadata().unwrap().len() > 0);
        let mut expected_hybrid_key_file_contents = Vec::new();
        File::open(&key_path)
            .unwrap()
            .read_to_end(&mut expected_hybrid_key_file_contents)
            .unwrap();

        let iv_path = tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME);
        assert!(iv_path.metadata().unwrap().len() == IOTEDGED_CRYPTO_IV_LEN_BYTES as u64);
        let mut expected_iv_file_contents = Vec::new();
        File::open(&iv_path)
            .unwrap()
            .read_to_end(&mut expected_iv_file_contents)
            .unwrap();

        let (force_module_reprovision, hybrid_identity_key) = prepare_master_hybrid_identity_key(
            &settings,
            &crypto,
            tmp_dir.path(),
            EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME,
            EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME,
            None,
        )
        .unwrap();

        // validate that no module reprovision is required since nothing changed
        assert!(!force_module_reprovision);

        // validate that the iv and hybrid keys were not changed on disk
        assert_eq!(
            expected_hybrid_identity_key.unwrap(),
            hybrid_identity_key.unwrap()
        );

        let mut file_contents = Vec::new();
        File::open(&key_path)
            .unwrap()
            .read_to_end(&mut file_contents)
            .unwrap();
        assert_eq!(expected_hybrid_key_file_contents, file_contents);
        let mut file_contents = Vec::new();
        File::open(&iv_path)
            .unwrap()
            .read_to_end(&mut file_contents)
            .unwrap();
        assert_eq!(expected_iv_file_contents, file_contents);
    }

    #[test]
    fn master_hybrid_id_key_creates_new_backup_and_iv_and_key_when_decrypt_fails() {
        let tmp_dir = TempDir::new("blah").unwrap();

        let stale_hybrid_key = vec![2; IDENTITY_MASTER_KEY_LEN_BYTES];
        let id_key_path = tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME);
        File::create(&id_key_path)
            .expect("Stale hybrid key file could not be created")
            .write_all(&stale_hybrid_key)
            .expect("Stale hybrid key file could not be written");

        let stale_iv = vec![1; IOTEDGED_CRYPTO_IV_LEN_BYTES];
        let iv_path = tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME);
        File::create(&iv_path)
            .expect("Stale iv file could not be created")
            .write_all(&stale_iv)
            .expect("Stale iv file could not be written");

        let cert_path = tmp_dir.path().join("test_cert");
        let cert_key_path = tmp_dir.path().join("test_key");
        let settings_path = tmp_dir.path().join("test_settings.yaml");

        prepare_test_dps_x509_settings_yaml(&settings_path, &cert_path, &cert_key_path);
        let settings = Settings::new(&settings_path).unwrap();

        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: true,
            fail_encrypt: false,
        };

        let (force_module_reprovision, hybrid_identity_key) = prepare_master_hybrid_identity_key(
            &settings,
            &crypto,
            tmp_dir.path(),
            EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME,
            EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME,
            None,
        )
        .unwrap();

        // validate that module reprovision is required since decrypt failed
        assert!(force_module_reprovision);
        assert_eq!(
            hybrid_identity_key.unwrap().len(),
            IDENTITY_MASTER_KEY_LEN_BYTES
        );

        // hybrid key and iv should be created and non empty since the auth mode is X.509
        assert!(id_key_path.metadata().unwrap().len() > 0);
        let mut hybrid_key_file_contents = Vec::new();
        File::open(&id_key_path)
            .unwrap()
            .read_to_end(&mut hybrid_key_file_contents)
            .unwrap();

        assert!(iv_path.metadata().unwrap().len() == IOTEDGED_CRYPTO_IV_LEN_BYTES as u64);
        let mut iv_file_contents = Vec::new();
        File::open(&iv_path)
            .unwrap()
            .read_to_end(&mut iv_file_contents)
            .unwrap();

        // validate that the iv and hybrid keys were changed on disk
        assert_ne!(stale_hybrid_key, hybrid_key_file_contents);
        assert_ne!(stale_hybrid_key, iv_file_contents);
    }

    #[test]
    fn master_hybrid_id_key_fails_when_encrypt_fails() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let cert_path = tmp_dir.path().join("test_cert");
        let key_path = tmp_dir.path().join("test_key");
        let settings_path = tmp_dir.path().join("test_settings.yaml");

        prepare_test_dps_x509_settings_yaml(&settings_path, &cert_path, &key_path);
        let settings = Settings::new(&settings_path).unwrap();

        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: true,
        };

        // validate that hyrbid id key create fails
        prepare_master_hybrid_identity_key(
            &settings,
            &crypto,
            tmp_dir.path(),
            EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME,
            EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME,
            None,
        )
        .unwrap_err();
    }

    #[test]
    fn master_hybrid_id_key_creates_new_backup_and_iv_when_iv_is_corrupted() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let cert_path = tmp_dir.path().join("test_cert");
        let key_path = tmp_dir.path().join("test_key");
        let settings_path = tmp_dir.path().join("test_settings.yaml");

        prepare_test_dps_x509_settings_yaml(&settings_path, &cert_path, &key_path);
        let settings = Settings::new(&settings_path).unwrap();

        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };

        let (force_module_reprovision, first_hybrid_identity_key) =
            prepare_master_hybrid_identity_key(
                &settings,
                &crypto,
                tmp_dir.path(),
                EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME,
                EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME,
                None,
            )
            .unwrap();

        // validate that module reprovision is required since this is the first time it was checked
        assert!(force_module_reprovision);
        assert_eq!(
            first_hybrid_identity_key.clone().unwrap().len(),
            IDENTITY_MASTER_KEY_LEN_BYTES
        );

        // hybrid key and iv should be created and non empty since the auth mode is X.509
        let key_path = tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME);
        let iv_path = tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME);
        assert!(key_path.metadata().unwrap().len() > 0);
        assert!(iv_path.metadata().unwrap().len() == IOTEDGED_CRYPTO_IV_LEN_BYTES as u64);

        // save off the first hybrid key file
        let mut first_key_file_contents = Vec::new();
        File::open(&key_path)
            .unwrap()
            .read_to_end(&mut first_key_file_contents)
            .unwrap();

        // save off the first iv file
        let mut first_iv_file_contents = Vec::new();
        File::open(&iv_path)
            .unwrap()
            .read_to_end(&mut first_iv_file_contents)
            .unwrap();

        // now corrupt the iv file by updating it with a non compliant byte length
        let corrupt_iv = vec![1; IOTEDGED_CRYPTO_IV_LEN_BYTES + 1];
        File::create(&iv_path)
            .expect("Corrupt iv file could not be created")
            .write_all(&corrupt_iv)
            .expect("Corrupt iv file could not be written");

        let (force_module_reprovision, second_hybrid_identity_key) =
            prepare_master_hybrid_identity_key(
                &settings,
                &crypto,
                tmp_dir.path(),
                EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME,
                EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME,
                None,
            )
            .unwrap();

        // validate that module reprovision is required since iv was corrupted
        assert!(force_module_reprovision);
        assert_eq!(
            second_hybrid_identity_key.clone().unwrap().len(),
            IDENTITY_MASTER_KEY_LEN_BYTES
        );

        // validate that a new hybrid id key was created
        assert_ne!(
            first_hybrid_identity_key.unwrap(),
            second_hybrid_identity_key.unwrap()
        );

        // validate that a new hybrid id key was generated and changed on disk
        assert!(key_path.metadata().unwrap().len() > 0);
        let mut second_hybrid_key_file_contents = Vec::new();
        File::open(&key_path)
            .unwrap()
            .read_to_end(&mut second_hybrid_key_file_contents)
            .unwrap();
        assert_ne!(first_key_file_contents, second_hybrid_key_file_contents);

        // validate that a new iv was created and changed on disk
        assert!(iv_path.metadata().unwrap().len() == IOTEDGED_CRYPTO_IV_LEN_BYTES as u64);
        let mut second_iv_file_contents = Vec::new();
        File::open(&iv_path)
            .unwrap()
            .read_to_end(&mut second_iv_file_contents)
            .unwrap();
        assert_ne!(first_iv_file_contents, second_iv_file_contents);
        assert_ne!(corrupt_iv, second_iv_file_contents);
    }

    #[test]
    fn master_hybrid_id_key_creates_new_backup_and_iv_when_hybrid_key_is_corrupted() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let cert_path = tmp_dir.path().join("test_cert");
        let key_path = tmp_dir.path().join("test_key");
        let settings_path = tmp_dir.path().join("test_settings.yaml");

        prepare_test_dps_x509_settings_yaml(&settings_path, &cert_path, &key_path);
        let settings = Settings::new(&settings_path).unwrap();

        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };

        let (force_module_reprovision, first_hybrid_identity_key) =
            prepare_master_hybrid_identity_key(
                &settings,
                &crypto,
                tmp_dir.path(),
                EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME,
                EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME,
                None,
            )
            .unwrap();

        // validate that module reprovision is required since this is the first time it was checked
        assert!(force_module_reprovision);
        assert_eq!(
            first_hybrid_identity_key.clone().unwrap().len(),
            IDENTITY_MASTER_KEY_LEN_BYTES
        );

        // hybrid key and iv should be created and non empty since the auth mode is X.509
        let key_path = tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME);
        let iv_path = tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME);
        assert!(key_path.metadata().unwrap().len() > 0);
        assert!(iv_path.metadata().unwrap().len() == IOTEDGED_CRYPTO_IV_LEN_BYTES as u64);

        // save off the first hybrid key file
        let mut first_key_file_contents = Vec::new();
        File::open(&key_path)
            .unwrap()
            .read_to_end(&mut first_key_file_contents)
            .unwrap();

        // save off the first iv file
        let mut first_iv_file_contents = Vec::new();
        File::open(&iv_path)
            .unwrap()
            .read_to_end(&mut first_iv_file_contents)
            .unwrap();

        // now corrupt the key file by updating it with a non compliant byte length
        let corrupt_key = vec![1; IDENTITY_MASTER_KEY_LEN_BYTES + 1];
        File::create(&key_path)
            .expect("Corrupt key file could not be created")
            .write_all(&corrupt_key)
            .expect("Corrupt key file could not be written");

        let (force_module_reprovision, second_hybrid_identity_key) =
            prepare_master_hybrid_identity_key(
                &settings,
                &crypto,
                tmp_dir.path(),
                EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME,
                EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME,
                None,
            )
            .unwrap();

        // validate that module reprovision is required since hybrid key was corrupted
        assert!(force_module_reprovision);
        assert_eq!(
            second_hybrid_identity_key.clone().unwrap().len(),
            IDENTITY_MASTER_KEY_LEN_BYTES
        );

        // validate that a new hybrid id key was created
        assert_ne!(
            first_hybrid_identity_key.unwrap(),
            second_hybrid_identity_key.unwrap()
        );

        // validate that a new hybrid id key was generated and changed on disk
        assert!(key_path.metadata().unwrap().len() > 0);
        let mut second_hybrid_key_file_contents = Vec::new();
        File::open(&key_path)
            .unwrap()
            .read_to_end(&mut second_hybrid_key_file_contents)
            .unwrap();
        assert_ne!(first_key_file_contents, second_hybrid_key_file_contents);
        assert_ne!(corrupt_key, second_hybrid_key_file_contents);

        // validate that a new iv was created and changed on disk
        assert!(iv_path.metadata().unwrap().len() == IOTEDGED_CRYPTO_IV_LEN_BYTES as u64);
        let mut second_iv_file_contents = Vec::new();
        File::open(&iv_path)
            .unwrap()
            .read_to_end(&mut second_iv_file_contents)
            .unwrap();
        assert_ne!(first_iv_file_contents, second_iv_file_contents);
    }

    #[test]
    fn master_hybrid_id_key_deletes_stale_hybrid_key_and_iv_when_provisioning_to_non_x509_auth() {
        let tmp_dir = TempDir::new("blah").unwrap();

        let hybrid_key_path = tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME);
        File::create(&hybrid_key_path)
            .expect("Stale hybrid key file could not be created")
            .write_all(b"jabba")
            .expect("Stale hybrid key file could not be written");

        let iv_path = tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME);
        File::create(&iv_path)
            .expect("Stale iv file could not be created")
            .write_all(b"jibba")
            .expect("Stale iv file could not be written");

        // prepare a non x509 provisioning configuration
        let settings = Settings::new(Path::new(GOOD_SETTINGS)).unwrap();

        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };

        let (force_module_reprovision, hybrid_identity_key) = prepare_master_hybrid_identity_key(
            &settings,
            &crypto,
            tmp_dir.path(),
            EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME,
            EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME,
            None,
        )
        .unwrap();

        // validate that module reprovision is not required this was not using x509
        assert!(!force_module_reprovision);
        // validate that no key was created
        assert!(hybrid_identity_key.is_none());

        // no hybrid key and iv should be created and any stale files deleted
        // since the auth mode is not X.509
        assert!(!tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME)
            .exists());
        assert!(!tmp_dir
            .path()
            .join(EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME)
            .exists());
    }
}
