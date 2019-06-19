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
use std::fs::{DirBuilder, File};
use std::io::Write;
use std::path::{Path, PathBuf};
use std::sync::Arc;

use failure::{Context, Fail, ResultExt};
use futures::future::{Either, IntoFuture};
use futures::sync::oneshot::{self, Receiver};
use futures::{future, Future};
use hyper::server::conn::Http;
use hyper::{Body, Request, Uri};
use log::{debug, info};
use serde::de::DeserializeOwned;
use serde::Serialize;
use sha2::{Digest, Sha256};
use url::Url;

use docker::models::HostConfig;
use dps::DPS_API_VERSION;
use edgelet_config::{
    AttestationMethod, Dps, Manual, Provisioning, Settings, SymmetricKeyAttestationInfo,
    TpmAttestationInfo, DEFAULT_CONNECTION_STRING,
};
use edgelet_core::crypto::{
    Activate, CreateCertificate, Decrypt, DerivedKeyStore, Encrypt, GetDeviceIdentityCertificate,
    GetIssuerAlias, GetTrustBundle, KeyIdentity, KeyStore, MakeRandom, MasterEncryptionKey,
    MemoryKey, MemoryKeyStore, Sign, Signature, SignatureAlgorithm, IOTEDGED_CA_ALIAS,
};
use edgelet_core::watchdog::Watchdog;
use edgelet_core::{
    Authenticator, Certificate, CertificateIssuer, CertificateProperties, CertificateType, Module,
    ModuleRuntime, ModuleRuntimeErrorReason, ModuleSpec, UrlExt, WorkloadConfig, UNIX_SCHEME,
};
use edgelet_docker::{DockerConfig, DockerModuleRuntime};
use edgelet_hsm::tpm::{TpmKey, TpmKeyStore};
use edgelet_hsm::{Crypto, HsmLock, X509};
use edgelet_http::certificate_manager::CertificateManager;
use edgelet_http::client::{Client as HttpClient, ClientImpl};
use edgelet_http::logging::LoggingService;
use edgelet_http::{HyperExt, MaybeProxyClient, PemCertificate, API_VERSION};
use edgelet_http_external_provisioning::ExternalProvisioningClient;
use edgelet_http_mgmt::ManagementService;
use edgelet_http_workload::WorkloadService;
use edgelet_iothub::{HubIdentityManager, SasTokenSource};
pub use error::{Error, ErrorKind, InitializeErrorReason};
use hsm::tpm::Tpm;
use hsm::ManageTpmKeys;
use iothubservice::DeviceClient;
use provisioning::provisioning::{
    AuthType, BackupProvisioning, DpsSymmetricKeyProvisioning, DpsTpmProvisioning,
    DpsX509Provisioning, ExternalProvisioning, ManualProvisioning, Provision, ProvisioningResult,
    ReprovisioningStatus,
};

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

/// This is the key for the docker network Id.
const EDGE_NETWORKID_KEY: &str = "NetworkId";

/// This is the key for the largest API version that this edgelet supports
const API_VERSION_KEY: &str = "IOTEDGE_APIVERSION";

const IOTHUB_API_VERSION: &str = "2017-11-08-preview";

/// This is the name of the provisioning backup file
const EDGE_PROVISIONING_BACKUP_FILENAME: &str = "provisioning_backup.json";

/// This is the name of the settings backup file
const EDGE_SETTINGS_STATE_FILENAME: &str = "settings_state";

/// This is the name of the hybrid X509-SAS key file
const EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME: &str = "iotedge_hybrid_key";
/// This is the name of the hybrid X509-SAS initialization vector
const EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME: &str = "iotedge_hybrid_iv";
/// Size in bytes of the master identity key
const IDENTITY_MASTER_KEY_LEN_BYTES: usize = 32;
/// Size in bytes of the initialization vector
const IOTEDGED_CRYPTO_IV_LEN_BYTES: usize = 16;
/// Identity to be used for various crypto operations
const IOTEDGED_CRYPTO_ID: &str = "$iotedge";

/// This is the name of the cache subdirectory for settings state
const EDGE_SETTINGS_SUBDIR: &str = "cache";

/// This is the DPS registration ID env variable key
const DPS_REGISTRATION_ID_ENV_KEY: &str = "IOTEDGE_REGISTRATION_ID";
/// This is the DPS identity certificate file path/uri env variable key
const DPS_DEVICE_ID_CERT_ENV_KEY: &str = "IOTEDGE_DEVICE_IDENTITY_CERT";
/// This is the DPS identity private key file path/uri env variable key
const DPS_DEVICE_ID_KEY_ENV_KEY: &str = "IOTEDGE_DEVICE_IDENTITY_PK";

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

pub struct Main {
    settings: Settings<DockerConfig>,
}

#[derive(PartialEq)]
enum ProvisioningAuthMethod {
    X509,
    SharedAccessKey,
}

struct IdentityCertificateData {
    common_name: String,
    thumbprint: String,
}

impl Main {
    pub fn new(settings: Settings<DockerConfig>) -> Self {
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

        info!(
            "Using runtime network id {}",
            settings.moby_runtime().network()
        );
        let runtime = DockerModuleRuntime::new(settings.moby_runtime().uri())
            .context(ErrorKind::Initialize(InitializeErrorReason::ModuleRuntime))?
            .with_network_id(settings.moby_runtime().network().to_string());

        init_docker_runtime(&runtime, &mut tokio_runtime)?;

        set_iot_edge_env_vars(&settings);

        info!("Initializing hsm...");
        let crypto = Crypto::new(hsm_lock.clone())
            .context(ErrorKind::Initialize(InitializeErrorReason::Hsm))?;
        info!("Finished initializing hsm.");

        let (hyper_client, device_cert_identity_data) =
            prepare_httpclient_and_identity_data(hsm_lock.clone(), &settings)?;

        // Detect if the settings were changed and if the device needs to be reconfigured
        let cache_subdir_path = Path::new(&settings.homedir()).join(EDGE_SETTINGS_SUBDIR);
        let hybrid_identity_key = check_settings_state(
            &cache_subdir_path.clone(),
            EDGE_SETTINGS_STATE_FILENAME,
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            EDGE_HYBRID_IDENTITY_MASTER_KEY_FILENAME,
            EDGE_HYBRID_IDENTITY_MASTER_KEY_IV_FILENAME,
        )?;

        macro_rules! start_edgelet {
            ($key_store:ident, $provisioning_result:ident, $root_key:ident, $runtime:ident) => {{
                info!("Finished provisioning edge device.");

                let cfg = WorkloadData::new(
                    $provisioning_result.hub_name().to_string(),
                    $provisioning_result.device_id().to_string(),
                    IOTEDGE_ID_CERT_MAX_DURATION_SECS,
                    IOTEDGE_SERVER_CERT_MAX_DURATION_SECS,
                );
                // This "do-while" loop runs until a StartApiReturnStatus::Shutdown
                // is received. If the TLS cert needs a restart, we will loop again.
                loop {
                    let code = start_api(
                        &settings,
                        hyper_client.clone(),
                        &$runtime,
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
                start_edgelet!(key_store, provisioning_result, root_key, runtime);
            }
            Provisioning::External(external) => {
                info!("Starting provisioning edge device via external provisioning mode...");
                let external_provisioning_client =
                    ExternalProvisioningClient::new(external.endpoint()).context(
                        ErrorKind::Initialize(InitializeErrorReason::ExternalProvisioningClient),
                    )?;
                let external_provisioning = ExternalProvisioning::new(external_provisioning_client);

                let provision_fut = external_provisioning
                    .provision(MemoryKeyStore::new())
                    .map_err(|err| {
                        Error::from(err.context(ErrorKind::Initialize(
                            InitializeErrorReason::ExternalProvisioningClient,
                        )))
                    });

                let prov_result = tokio_runtime.block_on(provision_fut)?;

                let credentials = if let Some(credentials) = prov_result.credentials() {
                    credentials
                } else {
                    info!("Credentials are expected to be populated for external provisioning.");

                    return Err(Error::from(ErrorKind::Initialize(
                        InitializeErrorReason::ExternalProvisioningClient,
                    )));
                };

                match credentials.auth_type() {
                    AuthType::SymmetricKey(symmetric_key) => {
                        if let Some(key) = symmetric_key.key() {
                            let (derived_key_store, memory_key) = external_provision_payload(key);
                            start_edgelet!(derived_key_store, prov_result, memory_key, runtime);
                        } else {
                            let (derived_key_store, tpm_key) =
                                external_provision_tpm(hsm_lock.clone())?;
                            start_edgelet!(derived_key_store, prov_result, tpm_key, runtime);
                        }
                    }
                    AuthType::X509(_) => {
                        info!("Unexpected auth type. Only symmetric keys are expected");
                        return Err(Error::from(ErrorKind::Initialize(
                            InitializeErrorReason::ExternalProvisioningClient,
                        )));
                    }
                };
            }
            Provisioning::Dps(dps) => {
                let dps_path = cache_subdir_path.join(EDGE_PROVISIONING_BACKUP_FILENAME);

                match dps.attestation() {
                    AttestationMethod::Tpm(ref tpm) => {
                        info!("Starting provisioning edge device via TPM...");
                        let (key_store, provisioning_result, root_key, runtime) =
                            dps_tpm_provision(
                                &dps,
                                hyper_client.clone(),
                                dps_path,
                                runtime,
                                &mut tokio_runtime,
                                tpm,
                                hsm_lock.clone(),
                            )?;
                        start_edgelet!(key_store, provisioning_result, root_key, runtime);
                    }
                    AttestationMethod::SymmetricKey(ref symmetric_key_info) => {
                        info!("Starting provisioning edge device via symmetric key...");
                        let (key_store, provisioning_result, root_key, runtime) =
                            dps_symmetric_key_provision(
                                &dps,
                                hyper_client.clone(),
                                dps_path,
                                runtime,
                                &mut tokio_runtime,
                                symmetric_key_info,
                            )?;
                        start_edgelet!(key_store, provisioning_result, root_key, runtime);
                    }
                    AttestationMethod::X509(ref x509_info) => {
                        info!("Starting provisioning edge device via X509 provisioning...");

                        let id_data = device_cert_identity_data.ok_or_else(|| {
                            ErrorKind::Initialize(InitializeErrorReason::DpsProvisioningClient)
                        })?;

                        // use the client provided registration id if provided else use the CN
                        let reg_id = match x509_info.registration_id() {
                            Some(id) => id.to_string(),
                            None => id_data.common_name,
                        };

                        let key_bytes = hybrid_identity_key.ok_or_else(|| {
                            ErrorKind::Initialize(InitializeErrorReason::DpsProvisioningClient)
                        })?;

                        let (key_store, provisioning_result, root_key, runtime) =
                            dps_x509_provision(
                                reg_id,
                                &dps,
                                hyper_client.clone(),
                                dps_path,
                                runtime,
                                &mut tokio_runtime,
                                &key_bytes,
                                id_data.thumbprint,
                            )?;
                        start_edgelet!(key_store, provisioning_result, root_key, runtime);
                    }
                }
            }
        };

        info!("Shutdown complete.");
        Ok(())
    }
}

fn set_iot_edge_env_vars(settings: &Settings<DockerConfig>) {
    info!(
        "Configuring {} as the home directory.",
        settings.homedir().display()
    );
    env::set_var(HOMEDIR_KEY, &settings.homedir());

    info!("Configuring certificates...");
    let certificates = &settings.certificates();
    match certificates.as_ref() {
        None => {
            info!("Transparent gateway certificates not found, operating in quick start mode...")
        }
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

    if let Provisioning::Dps(dps) = settings.provisioning() {
        match dps.attestation() {
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

                env::set_var(DPS_DEVICE_ID_CERT_ENV_KEY, x509_info.identity_cert());
                env::set_var(DPS_DEVICE_ID_KEY_ENV_KEY, x509_info.identity_pk());
            }
        }
    }
    info!("Finished configuring provisioning environment variables and certificates.");
}

fn prepare_httpclient_and_identity_data(
    hsm_lock: Arc<HsmLock>,
    settings: &Settings<DockerConfig>,
) -> Result<(MaybeProxyClient, Option<IdentityCertificateData>), Error> {
    if get_provisioning_auth_method(&settings) == ProvisioningAuthMethod::X509 {
        info!("Initializing hsm X509 interface...");
        let x509 =
            X509::new(hsm_lock).context(ErrorKind::Initialize(InitializeErrorReason::Hsm))?;

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
    } else {
        let hyper_client = MaybeProxyClient::new(get_proxy_uri(None)?, None, None)
            .context(ErrorKind::Initialize(InitializeErrorReason::HttpClient))?;

        Ok((hyper_client, None))
    }
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

fn check_x509_reprovisioning<C>(
    subdir_path: &PathBuf,
    settings: &Settings<DockerConfig>,
    crypto: &C,
    hybrid_id_filename: &str,
    iv_filename: &str,
) -> (bool, Option<Vec<u8>>)
where
    C: CreateCertificate + Decrypt,
{
    fn check_x509_reprovisioning_inner<C>(
        subdir_path: &PathBuf,
        crypto: &C,
        hybrid_id_filename: &str,
        iv_filename: &str,
    ) -> Result<(bool, Option<Vec<u8>>), HybridAuthError>
    where
        C: CreateCertificate + Decrypt,
    {
        // check if the identity key & iv files exist and are valid
        let key_path = subdir_path.join(hybrid_id_filename);
        let enc_identity_key = fs::read(key_path.as_path())?;
        let iv_path = subdir_path.join(iv_filename);
        let iv = fs::read(iv_path.as_path())?;
        if iv.len() == IOTEDGED_CRYPTO_IV_LEN_BYTES {
            let identity_key = crypto.decrypt(
                IOTEDGED_CRYPTO_ID.as_bytes(),
                enc_identity_key.as_ref(),
                &iv,
            )?;
            if identity_key.as_ref().len() == IDENTITY_MASTER_KEY_LEN_BYTES {
                Ok((false, Some(identity_key.as_ref().to_vec())))
            } else {
                Ok((true, None))
            }
        } else {
            Ok((true, None))
        }
    }

    if get_provisioning_auth_method(settings) == ProvisioningAuthMethod::X509 {
        check_x509_reprovisioning_inner(subdir_path, crypto, hybrid_id_filename, iv_filename)
            .unwrap_or((true, None))
    } else {
        (false, None)
    }
}

#[allow(clippy::too_many_arguments)]
fn check_settings_state<M, C>(
    subdir_path: &PathBuf,
    settings_state_filename: &str,
    settings: &Settings<DockerConfig>,
    runtime: &M,
    crypto: &C,
    tokio_runtime: &mut tokio::runtime::Runtime,
    hybrid_id_filename: &str,
    iv_filename: &str,
) -> Result<Option<Vec<u8>>, Error>
where
    M: ModuleRuntime,
    <M as ModuleRuntime>::RemoveAllFuture: 'static,
    C: CreateCertificate + Decrypt + Encrypt + GetIssuerAlias + MasterEncryptionKey + MakeRandom,
{
    info!("Detecting if configuration file has changed...");
    let (mut reconfig_reqd, mut hybrid_id_key) = check_x509_reprovisioning(
        subdir_path,
        settings,
        crypto,
        hybrid_id_filename,
        iv_filename,
    );
    if !reconfig_reqd {
        let path = subdir_path.join(settings_state_filename);
        let diff = settings.diff_with_cached(&path);
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
    }

    if reconfig_reqd {
        hybrid_id_key = reconfigure(
            &subdir_path,
            settings_state_filename,
            settings,
            runtime,
            crypto,
            tokio_runtime,
            hybrid_id_filename,
            iv_filename,
        )?;
    }
    Ok(hybrid_id_key)
}

fn get_provisioning_auth_method(settings: &Settings<DockerConfig>) -> ProvisioningAuthMethod {
    let mut method = ProvisioningAuthMethod::SharedAccessKey;
    if let Provisioning::Dps(dps) = settings.provisioning() {
        if let AttestationMethod::X509(_) = dps.attestation() {
            method = ProvisioningAuthMethod::X509;
        }
    }
    method
}

#[allow(clippy::too_many_arguments)]
fn reconfigure<M, C>(
    subdir: &PathBuf,
    settings_state_filename: &str,
    settings: &Settings<DockerConfig>,
    runtime: &M,
    crypto: &C,
    tokio_runtime: &mut tokio::runtime::Runtime,
    hybrid_id_filename: &str,
    iv_filename: &str,
) -> Result<(Option<Vec<u8>>), Error>
where
    M: ModuleRuntime,
    <M as ModuleRuntime>::RemoveAllFuture: 'static,
    C: CreateCertificate + Decrypt + Encrypt + GetIssuerAlias + MasterEncryptionKey + MakeRandom,
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

    let path = subdir.join(settings_state_filename);

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
    let digest = settings
        .compute_settings_digest()
        .context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;
    file.write_all(digest.as_bytes())
        .context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;

    let mut identity_master_key: Option<Vec<u8>> = None;
    if get_provisioning_auth_method(settings) == ProvisioningAuthMethod::X509 {
        let mut key_bytes: [u8; IDENTITY_MASTER_KEY_LEN_BYTES] = [0; IDENTITY_MASTER_KEY_LEN_BYTES];
        crypto
            .get_random_bytes(&mut key_bytes)
            .context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;

        let mut iv: [u8; IOTEDGED_CRYPTO_IV_LEN_BYTES] = [0; IOTEDGED_CRYPTO_IV_LEN_BYTES];
        crypto
            .get_random_bytes(&mut iv)
            .context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;

        let enc_identity_key = crypto
            .encrypt(IOTEDGED_CRYPTO_ID.as_bytes(), &key_bytes, &iv)
            .context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;

        let path = subdir.clone().join(hybrid_id_filename);
        let mut file = File::create(path)
            .context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;
        file.write_all(enc_identity_key.as_bytes())
            .context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;

        let path = subdir.join(iv_filename);
        let mut file = File::create(path)
            .context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;
        file.write_all(&iv)
            .context(ErrorKind::Initialize(InitializeErrorReason::SaveSettings))?;

        identity_master_key = Some(key_bytes.to_vec())
    }

    Ok(identity_master_key)
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

    let mgmt = start_management(&settings, runtime, &id_man, mgmt_rx, cert_manager.clone());

    let workload = start_workload(
        &settings,
        key_store,
        runtime,
        work_rx,
        crypto,
        cert_manager,
        workload_config,
    );

    let (runt_tx, runt_rx) = oneshot::channel();
    let edge_rt = start_runtime(&runtime, &id_man, &hub_name, &device_id, &settings, runt_rx)?;

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

#[allow(clippy::too_many_arguments)]
fn dps_x509_provision<HC, M>(
    reg_id: String,
    provisioning: &Dps,
    hyper_client: HC,
    backup_path: PathBuf,
    runtime: M,
    tokio_runtime: &mut tokio::runtime::Runtime,
    hybrid_identity_key: &[u8],
    cert_thumbprint: String,
) -> Result<(DerivedKeyStore<MemoryKey>, ProvisioningResult, MemoryKey, M), Error>
where
    HC: 'static + ClientImpl,
    M: ModuleRuntime + Send + 'static,
{
    let mut memory_hsm = MemoryKeyStore::new();

    memory_hsm
        .activate_identity_key(
            KeyIdentity::Device,
            "primary".to_string(),
            hybrid_identity_key,
        )
        .context(ErrorKind::ActivateSymmetricKey)?;

    let dps = DpsX509Provisioning::new(
        hyper_client,
        provisioning.global_endpoint().clone(),
        provisioning.scope_id().to_string(),
        reg_id,
        DPS_API_VERSION.to_string(),
    )
    .context(ErrorKind::Initialize(
        InitializeErrorReason::DpsProvisioningClient,
    ))?;

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
            if prov_result.reconfigure() == ReprovisioningStatus::DeviceDataNotUpdated {
                Either::B(future::ok((prov_result, runtime)))
            } else {
                info!(
                    "Reprovisioning status {:?} will trigger reconfiguration of modules.",
                    prov_result.reconfigure()
                );

                let remove = runtime.remove_all().then(|result| {
                    result.context(ErrorKind::Initialize(
                        InitializeErrorReason::DpsProvisioningClient,
                    ))?;
                    Ok((prov_result, runtime))
                });
                Either::A(remove)
            }
        })
        .and_then(move |(prov_result, runtime)| {
            let (derived_key_store, hybrid_derived_key) = prepare_hybrid_key(
                &memory_hsm,
                &cert_thumbprint,
                prov_result.hub_name(),
                prov_result.device_id(),
            )?;
            Ok((derived_key_store, prov_result, hybrid_derived_key, runtime))
        });
    tokio_runtime.block_on(provision)
}

fn prepare_hybrid_key(
    key_store: &MemoryKeyStore,
    cert_thumbprint: &str,
    hub_name: &str,
    device_id: &str,
) -> Result<(DerivedKeyStore<MemoryKey>, MemoryKey), Error> {
    let k = key_store
        .get(&KeyIdentity::Device, "primary")
        .context(ErrorKind::Initialize(
            InitializeErrorReason::DpsProvisioningClient,
        ))?;
    let sign_data = format!("{}{}{}", hub_name, device_id, cert_thumbprint);
    let digest = k
        .sign(SignatureAlgorithm::HMACSHA256, sign_data.as_bytes())
        .context(ErrorKind::Initialize(
            InitializeErrorReason::DpsProvisioningClient,
        ))?;
    let hybrid_derived_key = MemoryKey::new(digest.as_bytes());
    let derived_key_store = DerivedKeyStore::new(hybrid_derived_key.clone());
    Ok((derived_key_store, hybrid_derived_key))
}

fn external_provision_payload(key: &str) -> (DerivedKeyStore<MemoryKey>, MemoryKey) {
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
        InitializeErrorReason::ExternalProvisioningClient,
    ))?;

    let tpm_hsm = TpmKeyStore::from_hsm(tpm, hsm_lock).context(ErrorKind::Initialize(
        InitializeErrorReason::ExternalProvisioningClient,
    ))?;

    tpm_hsm
        .get(&KeyIdentity::Device, "primary")
        .map_err(|err| {
            Error::from(err.context(ErrorKind::Initialize(
                InitializeErrorReason::ExternalProvisioningClient,
            )))
        })
        .and_then(|k| {
            let derived_key_store = DerivedKeyStore::new(k.clone());
            Ok((derived_key_store, k))
        })
}

fn dps_symmetric_key_provision<HC, M>(
    provisioning: &Dps,
    hyper_client: HC,
    backup_path: PathBuf,
    runtime: M,
    tokio_runtime: &mut tokio::runtime::Runtime,
    key: &SymmetricKeyAttestationInfo,
) -> Result<(DerivedKeyStore<MemoryKey>, ProvisioningResult, MemoryKey, M), Error>
where
    HC: 'static + ClientImpl,
    M: ModuleRuntime + Send + 'static,
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
                if prov_result.reconfigure() == ReprovisioningStatus::DeviceDataNotUpdated {
                    Either::B(future::ok((prov_result, runtime)))
                } else {
                    // If there was a DPS reprovision and device key was updated results in obsolete
                    // module keys in IoTHub from the previous provisioning. We delete all containers
                    // after each DPS provisioning run so that IoTHub can be updated with new module
                    // keys when the deployment is executed by EdgeAgent.
                    info!(
                        "Reprovisioning status {:?} will trigger reconfiguration of modules.",
                        prov_result.reconfigure()
                    );

                    let remove = runtime.remove_all().then(|result| {
                        result.context(ErrorKind::Initialize(
                            InitializeErrorReason::DpsProvisioningClient,
                        ))?;
                        Ok((prov_result, runtime))
                    });
                    Either::A(remove)
                }
            })
            .and_then(move |(prov_result, runtime)| {
                let k = memory_hsm.get(&KeyIdentity::Device, "primary").context(
                    ErrorKind::Initialize(InitializeErrorReason::DpsProvisioningClient),
                )?;
                let derived_key_store = DerivedKeyStore::new(k.clone());
                Ok((derived_key_store, prov_result, k, runtime))
            });
    tokio_runtime.block_on(provision)
}

fn dps_tpm_provision<HC, M>(
    provisioning: &Dps,
    hyper_client: HC,
    backup_path: PathBuf,
    runtime: M,
    tokio_runtime: &mut tokio::runtime::Runtime,
    tpm_attestation_info: &TpmAttestationInfo,
    hsm_lock: Arc<HsmLock>,
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
        .and_then(|prov_result| {
            if prov_result.reconfigure() == ReprovisioningStatus::DeviceDataNotUpdated {
                Either::B(future::ok((prov_result, runtime)))
            } else {
                info!("Successful DPS provisioning. This will trigger reconfiguration of modules.");
                // Each time DPS reprovisions, it gets back a new device key. This results in obsolete
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
        spec.image_pull_policy(),
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

    let watchdog = Watchdog::new(
        runtime.clone(),
        id_man.clone(),
        settings.watchdog().max_retries().clone(),
    );
    let runtime_future = watchdog
        .run_until(spec, EDGE_RUNTIME_MODULEID, shutdown.map_err(|_| ()))
        .map_err(Error::from);

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

fn start_management<C, K, HC, M>(
    settings: &Settings<DockerConfig>,
    runtime: &M,
    id_man: &HubIdentityManager<DerivedKeyStore<K>, HC, K>,
    shutdown: Receiver<()>,
    cert_manager: Arc<CertificateManager<C>>,
) -> impl Future<Item = (), Error = Error>
where
    C: CreateCertificate + Clone,
    K: 'static + Sign + Clone + Send + Sync,
    HC: 'static + ClientImpl + Send + Sync,
    M: ModuleRuntime + Authenticator<Request = Request<Body>> + Send + Sync + Clone + 'static,
    <M::AuthenticateFuture as Future>::Error: Fail,
    for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
    M::Logs: Into<Body>,
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
    settings: &Settings<DockerConfig>,
    key_store: &K,
    runtime: &M,
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
    M: ModuleRuntime + Authenticator<Request = Request<Body>> + Send + Sync + Clone + 'static,
    <M::AuthenticateFuture as Future>::Error: Fail,
    for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
    <M::Module as Module>::Config: DeserializeOwned + Serialize,
    M::Logs: Into<Body>,
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

#[derive(Debug, Fail)]
#[fail(display = "Could not load settings")]
struct HybridAuthError(#[cause] Context<Box<dyn std::fmt::Display + Send + Sync>>);

impl From<std::io::Error> for HybridAuthError {
    fn from(err: std::io::Error) -> Self {
        HybridAuthError(Context::new(Box::new(err)))
    }
}

impl From<edgelet_core::Error> for HybridAuthError {
    fn from(err: edgelet_core::Error) -> Self {
        HybridAuthError(Context::new(Box::new(err)))
    }
}

#[cfg(test)]
mod tests {
    use std::fmt;
    use std::io::Read;
    use std::path::Path;

    use chrono::{Duration, Utc};
    use rand::RngCore;
    use tempdir::TempDir;

    use edgelet_core::ModuleRuntimeState;
    use edgelet_core::{KeyBytes, PrivateKey};
    use edgelet_test_utils::cert::TestCert;
    use edgelet_test_utils::module::*;

    use super::*;

    #[cfg(unix)]
    static SETTINGS: &str = "../edgelet-config/test/linux/sample_settings.yaml";
    #[cfg(unix)]
    static SETTINGS1: &str = "../edgelet-config/test/linux/sample_settings1.yaml";

    #[cfg(windows)]
    static SETTINGS: &str = "../edgelet-config/test/windows/sample_settings.yaml";
    #[cfg(windows)]
    static SETTINGS1: &str = "../edgelet-config/test/windows/sample_settings1.yaml";

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

    #[test]
    fn default_settings_raise_unconfigured_error() {
        let settings = Settings::<DockerConfig>::new(None).unwrap();
        let main = Main::new(settings);
        let result = main.run_until(signal::shutdown);
        match result.unwrap_err().kind() {
            ErrorKind::Initialize(InitializeErrorReason::NotConfigured) => (),
            kind => panic!("Expected `NotConfigured` but got {:?}", kind),
        }
    }

    #[test]
    fn settings_with_invalid_issuer_ca_fails() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::<DockerConfig>::new(Some(Path::new(SETTINGS))).unwrap();
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: true,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        let result = check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        );
        match result.unwrap_err().kind() {
            ErrorKind::Initialize(InitializeErrorReason::PrepareWorkloadCa) => (),
            kind => panic!("Expected `PrepareWorkloadCa` but got {:?}", kind),
        }
    }

    #[test]
    fn settings_with_expired_issuer_ca_fails() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::<DockerConfig>::new(Some(Path::new(SETTINGS))).unwrap();
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: true,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        let result = check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        );
        match result.unwrap_err().kind() {
            ErrorKind::Initialize(InitializeErrorReason::IssuerCAExpiration) => (),
            kind => panic!("Expected `IssuerCAExpiration` but got {:?}", kind),
        }
    }

    #[test]
    fn settings_non_x509_auth_first_time_creates_backup() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::<DockerConfig>::new(Some(Path::new(SETTINGS))).unwrap();
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        let result = check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();

        assert!(result.is_none());
        let expected_base64 = settings.compute_settings_digest().unwrap();
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();
        assert_eq!(expected_base64, written);

        // no hybrid key and iv should be created since the auth mode is not X.509
        let f = File::open(tmp_dir.path().join("iotedge_hybrid_key"));
        assert!(f.is_err());
        let f = File::open(tmp_dir.path().join("iotedge_hybrid_iv"));
        assert!(f.is_err());
    }

    #[test]
    fn settings_with_dps_x509_first_time_creates_backup() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let tmp_certs_dir = TempDir::new("certs").unwrap();
        let cert_path = tmp_certs_dir.path().join("test_cert");
        let key_path = tmp_certs_dir.path().join("test_key");
        File::create(&cert_path)
            .expect("Test cert file could not be created")
            .write_all(b"CN=Mr. T")
            .expect("Test cert file could not be written");
        File::create(&key_path)
            .expect("Test cert private key file could not be created")
            .write_all(b"i pity the fool")
            .expect("Test cert private key file could not be written");
        let settings_path = tmp_dir.path().join("test_settings.yaml");
        let settings_yaml = format!(
            "
provisioning:
  source: \"dps\"
  global_endpoint: \"scheme://jibba-jabba.net\"
  scope_id: \"i got no time for the jibba-jabba\"
  attestation:
    method: \"x509\"
    identity_pk: {}
    identity_cert: {}",
            key_path.to_str().unwrap(),
            cert_path.to_str().unwrap()
        );
        File::create(&settings_path)
            .expect("Settings file could not be created")
            .write_all(settings_yaml.as_bytes())
            .expect("Settings file could not be written");
        let settings =
            Settings::<DockerConfig>::new(Some(&settings_path)).expect("Invalid settings file");
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();

        let result = check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();

        let hybrid_key = result.unwrap();
        assert!(hybrid_key.len() == IDENTITY_MASTER_KEY_LEN_BYTES);

        let expected_base64 = settings.compute_settings_digest().unwrap();
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();
        assert_eq!(expected_base64, written);

        // hybrid key and iv should be created since the auth mode is not X.509
        let mut written = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_key"))
            .unwrap()
            .read_to_end(&mut written)
            .unwrap();
        assert_eq!(written.len(), IDENTITY_MASTER_KEY_LEN_BYTES);

        let mut written = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_iv"))
            .unwrap()
            .read_to_end(&mut written)
            .unwrap();
        assert!(written.len() == IOTEDGED_CRYPTO_IV_LEN_BYTES);
    }

    #[test]
    fn settings_with_dps_x509_with_new_config_creates_new_backup_and_iv_and_key() {
        let tmp_dir = TempDir::new("blah").unwrap();

        let stale_iv = vec![1; IOTEDGED_CRYPTO_IV_LEN_BYTES];
        let iv_path = tmp_dir.path().join("iotedge_hybrid_iv");
        File::create(&iv_path)
            .expect("Stale iv file could not be created")
            .write_all(&stale_iv)
            .expect("Stale iv file could not be written");

        let stale_hybrid_key = vec![2; IDENTITY_MASTER_KEY_LEN_BYTES];
        let hybrid_key_path = tmp_dir.path().join("iotedge_hybrid_key");
        File::create(&hybrid_key_path)
            .expect("Stale hybrid key file could not be created")
            .write_all(&stale_hybrid_key)
            .expect("Stale hybrid key file could not be written");

        let tmp_certs_dir = TempDir::new("certs").unwrap();
        let cert_path = tmp_certs_dir.path().join("test_cert");
        let key_path = tmp_certs_dir.path().join("test_key");
        File::create(&cert_path)
            .expect("Test cert file could not be created")
            .write_all(b"CN=Mr. T")
            .expect("Test cert file could not be written");
        File::create(&key_path)
            .expect("Test cert private key file could not be created")
            .write_all(b"i pity the fool")
            .expect("Test cert private key file could not be written");
        let settings_path = tmp_dir.path().join("test_settings.yaml");
        let settings_yaml = format!(
            "
provisioning:
  source: \"dps\"
  global_endpoint: \"scheme://jibba-jabba.net\"
  scope_id: \"i got no time for the jibba-jabba\"
  attestation:
    method: \"x509\"
    identity_pk: {}
    identity_cert: {}",
            key_path.to_str().unwrap(),
            cert_path.to_str().unwrap()
        );
        File::create(&settings_path)
            .expect("Settings file could not be created")
            .write_all(settings_yaml.as_bytes())
            .expect("Settings file could not be written");
        let settings =
            Settings::<DockerConfig>::new(Some(&settings_path)).expect("Invalid settings file");
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();

        let result = check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();

        // validate new hybrid key was created and different from the stale key
        let hybrid_key = result.unwrap();
        assert!(hybrid_key.len() == IDENTITY_MASTER_KEY_LEN_BYTES);
        assert_ne!(stale_hybrid_key, hybrid_key);

        let expected_base64 = settings.compute_settings_digest().unwrap();
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();
        assert_eq!(expected_base64, written);

        // hybrid key and iv files should be created since the auth mode is not X.509
        let mut written = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_key"))
            .unwrap()
            .read_to_end(&mut written)
            .unwrap();
        assert_eq!(hybrid_key, written.as_bytes());

        let mut written = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_iv"))
            .unwrap()
            .read_to_end(&mut written)
            .unwrap();
        assert!(written.len() == IOTEDGED_CRYPTO_IV_LEN_BYTES);

        // validate new iv was created and different from the stale iv
        assert_ne!(stale_iv, written);
    }

    #[test]
    fn settings_with_dps_x509_subsequent_checks_does_not_reconfigure() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let tmp_certs_dir = TempDir::new("certs").unwrap();
        let cert_path = tmp_certs_dir.path().join("test_cert");
        let key_path = tmp_certs_dir.path().join("test_key");
        File::create(&cert_path)
            .expect("Test cert file could not be created")
            .write_all(b"CN=Mr. T")
            .expect("Test cert file could not be written");
        File::create(&key_path)
            .expect("Test cert private key file could not be created")
            .write_all(b"i pity the fool")
            .expect("Test cert private key file could not be written");
        let settings_path = tmp_dir.path().join("test_settings.yaml");
        let settings_yaml = format!(
            "
provisioning:
  source: \"dps\"
  global_endpoint: \"scheme://jibba-jabba.net\"
  scope_id: \"i got no time for the jibba-jabba\"
  attestation:
    method: \"x509\"
    identity_pk: {}
    identity_cert: {}",
            key_path.to_str().unwrap(),
            cert_path.to_str().unwrap()
        );
        File::create(&settings_path)
            .expect("Settings file could not be created")
            .write_all(settings_yaml.as_bytes())
            .expect("Settings file could not be written");
        let settings =
            Settings::<DockerConfig>::new(Some(&settings_path)).expect("Invalid settings file");
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();

        let result = check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();

        let hybrid_key = result.unwrap();
        assert!(hybrid_key.len() == IDENTITY_MASTER_KEY_LEN_BYTES);

        let mut expected_hybrid_key_file_contents = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_key"))
            .unwrap()
            .read_to_end(&mut expected_hybrid_key_file_contents)
            .unwrap();

        let mut expected_iv_file_contents = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_iv"))
            .unwrap()
            .read_to_end(&mut expected_iv_file_contents)
            .unwrap();

        let expected_base64 = settings.compute_settings_digest().unwrap();

        let result = check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();

        // validate that the hybrid key was not changed
        let hybrid_key1 = result.unwrap();
        assert_eq!(hybrid_key, hybrid_key1);

        // validate that the settings digest was not changed
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();
        assert_eq!(expected_base64, written);

        // validate that the iv and hybrid keys were not changed
        let mut written = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_key"))
            .unwrap()
            .read_to_end(&mut written)
            .unwrap();
        assert_eq!(expected_hybrid_key_file_contents, written);

        let mut written = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_iv"))
            .unwrap()
            .read_to_end(&mut written)
            .unwrap();
        assert_eq!(expected_iv_file_contents, written);
    }

    #[test]
    fn settings_with_dps_x509_decrypt_fails_creates_new_backup_and_iv_and_key() {
        let tmp_dir = TempDir::new("blah").unwrap();

        let stale_iv = vec![1; IOTEDGED_CRYPTO_IV_LEN_BYTES];
        let iv_path = tmp_dir.path().join("iotedge_hybrid_iv");
        File::create(&iv_path)
            .expect("Stale iv file could not be created")
            .write_all(&stale_iv)
            .expect("Stale iv file could not be written");

        let stale_hybrid_key = vec![2; IDENTITY_MASTER_KEY_LEN_BYTES];
        let hybrid_key_path = tmp_dir.path().join("iotedge_hybrid_key");
        File::create(&hybrid_key_path)
            .expect("Stale hybrid key file could not be created")
            .write_all(&stale_hybrid_key)
            .expect("Stale hybrid key file could not be written");

        let tmp_certs_dir = TempDir::new("certs").unwrap();
        let cert_path = tmp_certs_dir.path().join("test_cert");
        let key_path = tmp_certs_dir.path().join("test_key");
        File::create(&cert_path)
            .expect("Test cert file could not be created")
            .write_all(b"CN=Mr. T")
            .expect("Test cert file could not be written");
        File::create(&key_path)
            .expect("Test cert private key file could not be created")
            .write_all(b"i pity the fool")
            .expect("Test cert private key file could not be written");

        let settings_path = tmp_dir.path().join("test_settings.yaml");
        let settings_yaml = format!(
            "
provisioning:
  source: \"dps\"
  global_endpoint: \"scheme://jibba-jabba.net\"
  scope_id: \"i got no time for the jibba-jabba\"
  attestation:
    method: \"x509\"
    identity_pk: {}
    identity_cert: {}",
            key_path.to_str().unwrap(),
            cert_path.to_str().unwrap()
        );
        File::create(&settings_path)
            .expect("Settings file could not be created")
            .write_all(settings_yaml.as_bytes())
            .expect("Settings file could not be written");
        let settings =
            Settings::<DockerConfig>::new(Some(&settings_path)).expect("Invalid settings file");
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: true,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        let result = check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();

        // validate new hybrid key was created and different from the stale key
        let hybrid_key = result.unwrap();
        assert!(hybrid_key.len() == IDENTITY_MASTER_KEY_LEN_BYTES);
        assert_ne!(stale_hybrid_key, hybrid_key);

        let expected_base64 = settings.compute_settings_digest().unwrap();
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();
        assert_eq!(expected_base64, written);

        // hybrid key and iv should be created since the auth mode is not X.509
        let mut written = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_key"))
            .unwrap()
            .read_to_end(&mut written)
            .unwrap();
        assert_eq!(hybrid_key, written.as_bytes());

        let mut written = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_iv"))
            .unwrap()
            .read_to_end(&mut written)
            .unwrap();
        assert!(written.len() == IOTEDGED_CRYPTO_IV_LEN_BYTES);

        // validate new iv was created and different from the stale iv
        assert_ne!(stale_iv, written);
    }

    #[test]
    fn settings_with_dps_x509_corrupted_iv_creates_new_backup_and_iv_and_key() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let tmp_certs_dir = TempDir::new("certs").unwrap();
        let cert_path = tmp_certs_dir.path().join("test_cert");
        let key_path = tmp_certs_dir.path().join("test_key");
        File::create(&cert_path)
            .expect("Test cert file could not be created")
            .write_all(b"CN=Mr. T")
            .expect("Test cert file could not be written");
        File::create(&key_path)
            .expect("Test cert private key file could not be created")
            .write_all(b"i pity the fool")
            .expect("Test cert private key file could not be written");

        let settings_path = tmp_dir.path().join("test_settings.yaml");
        let settings_yaml = format!(
            "
provisioning:
  source: \"dps\"
  global_endpoint: \"scheme://jibba-jabba.net\"
  scope_id: \"i got no time for the jibba-jabba\"
  attestation:
    method: \"x509\"
    identity_pk: {}
    identity_cert: {}",
            key_path.to_str().unwrap(),
            cert_path.to_str().unwrap()
        );
        File::create(&settings_path)
            .expect("Settings file could not be created")
            .write_all(settings_yaml.as_bytes())
            .expect("Settings file could not be written");
        let settings =
            Settings::<DockerConfig>::new(Some(&settings_path)).expect("Invalid settings file");
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        let result = check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();

        // validate new hybrid key was created
        let hybrid_key = result.unwrap();
        assert!(hybrid_key.len() == IDENTITY_MASTER_KEY_LEN_BYTES);
        let expected_base64 = settings.compute_settings_digest().unwrap();

        // save off prior iv
        let mut prior_iv = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_iv"))
            .unwrap()
            .read_to_end(&mut prior_iv)
            .unwrap();
        assert!(prior_iv.len() == IOTEDGED_CRYPTO_IV_LEN_BYTES);

        // make iv length different from expected to corrupt file
        let corrupt_iv = vec![1; IOTEDGED_CRYPTO_IV_LEN_BYTES + 1];
        let iv_path = tmp_dir.path().join("iotedge_hybrid_iv");
        File::create(&iv_path)
            .expect("Corrupt iv file could not be created")
            .write_all(&corrupt_iv)
            .expect("Corrupt iv file could not be written");

        // this reconfigures the provisioning info
        let result = check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();

        // validate new hybrid key was created
        let hybrid_key1 = result.unwrap();
        assert!(hybrid_key1.len() == IDENTITY_MASTER_KEY_LEN_BYTES);
        assert_ne!(hybrid_key, hybrid_key1);

        // validate most recent digest has not changed
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();
        assert_eq!(expected_base64, written);

        // validate new hybrid key was written correctly
        let mut written = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_key"))
            .unwrap()
            .read_to_end(&mut written)
            .unwrap();
        assert_eq!(hybrid_key1, written.as_bytes());

        // validate new iv key created and different from prior and corrupt iv
        let mut written = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_iv"))
            .unwrap()
            .read_to_end(&mut written)
            .unwrap();
        assert!(written.len() == IOTEDGED_CRYPTO_IV_LEN_BYTES);
        assert_ne!(prior_iv, written);
        assert_ne!(corrupt_iv, written);
    }

    #[test]
    fn settings_with_dps_x509_corrupted_hybrid_key_creates_new_backup_and_iv_and_key() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let tmp_certs_dir = TempDir::new("certs").unwrap();
        let cert_path = tmp_certs_dir.path().join("test_cert");
        let key_path = tmp_certs_dir.path().join("test_key");
        File::create(&cert_path)
            .expect("Test cert file could not be created")
            .write_all(b"CN=Mr. T")
            .expect("Test cert file could not be written");
        File::create(&key_path)
            .expect("Test cert private key file could not be created")
            .write_all(b"i pity the fool")
            .expect("Test cert private key file could not be written");

        let settings_path = tmp_dir.path().join("test_settings.yaml");
        let settings_yaml = format!(
            "
provisioning:
  source: \"dps\"
  global_endpoint: \"scheme://jibba-jabba.net\"
  scope_id: \"i got no time for the jibba-jabba\"
  attestation:
    method: \"x509\"
    identity_pk: {}
    identity_cert: {}",
            key_path.to_str().unwrap(),
            cert_path.to_str().unwrap()
        );
        File::create(&settings_path)
            .expect("Settings file could not be created")
            .write_all(settings_yaml.as_bytes())
            .expect("Settings file could not be written");
        let settings =
            Settings::<DockerConfig>::new(Some(&settings_path)).expect("Invalid settings file");
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        let result = check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();

        // validate new hybrid key was created
        let hybrid_key = result.unwrap();
        assert!(hybrid_key.len() == IDENTITY_MASTER_KEY_LEN_BYTES);
        let expected_base64 = settings.compute_settings_digest().unwrap();

        // save off prior iv
        let mut prior_iv = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_iv"))
            .unwrap()
            .read_to_end(&mut prior_iv)
            .unwrap();
        assert!(prior_iv.len() == IOTEDGED_CRYPTO_IV_LEN_BYTES);

        // save off prior key
        let mut prior_key = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_key"))
            .unwrap()
            .read_to_end(&mut prior_key)
            .unwrap();
        // since encrypt decrypt are pass thrus we should expect the same key on disk
        assert_eq!(hybrid_key, prior_key);

        // make hyrbid key length different from expected to corrupt file
        let corrupt_key = vec![1; IDENTITY_MASTER_KEY_LEN_BYTES + 1];
        let key_path = tmp_dir.path().join("iotedge_hybrid_key");
        File::create(&key_path)
            .expect("Corrupt key file could not be created")
            .write_all(&corrupt_key)
            .expect("Corrupt key file could not be written");

        // this reconfigures the provisioning info
        let result = check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();

        // validate new hybrid key was created
        let hybrid_key1 = result.unwrap();
        assert!(hybrid_key1.len() == IDENTITY_MASTER_KEY_LEN_BYTES);
        assert_ne!(hybrid_key, hybrid_key1);

        // validate most recent digest has not changed
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();
        assert_eq!(expected_base64, written);

        // validate new hybrid key was written correctly
        let mut written = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_key"))
            .unwrap()
            .read_to_end(&mut written)
            .unwrap();
        assert_eq!(hybrid_key1, written.as_bytes());
        assert_ne!(prior_key, written);
        assert_ne!(corrupt_key, written);

        // validate new iv key created and different from prior and corrupt iv
        let mut written = Vec::new();
        File::open(tmp_dir.path().join("iotedge_hybrid_iv"))
            .unwrap()
            .read_to_end(&mut written)
            .unwrap();
        assert!(written.len() == IOTEDGED_CRYPTO_IV_LEN_BYTES);
        assert_ne!(prior_iv, written);
    }

    #[test]
    fn settings_with_dps_x509_fails_if_encrypt_fails() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let tmp_certs_dir = TempDir::new("certs").unwrap();
        let cert_path = tmp_certs_dir.path().join("test_cert");
        let key_path = tmp_certs_dir.path().join("test_key");
        File::create(&cert_path)
            .expect("Test cert file could not be created")
            .write_all(b"CN=Mr. T")
            .expect("Test cert file could not be written");
        File::create(&key_path)
            .expect("Test cert private key file could not be created")
            .write_all(b"i pity the fool")
            .expect("Test cert private key file could not be written");
        let settings_path = tmp_dir.path().join("test_settings.yaml");
        let settings_yaml = format!(
            "
provisioning:
  source: \"dps\"
  global_endpoint: \"scheme://jibba-jabba.net\"
  scope_id: \"i got no time for the jibba-jabba\"
  attestation:
    method: \"x509\"
    identity_pk: {}
    identity_cert: {}",
            key_path.to_str().unwrap(),
            cert_path.to_str().unwrap()
        );
        File::create(&settings_path)
            .expect("Settings file could not be created")
            .write_all(settings_yaml.as_bytes())
            .expect("Settings file could not be written");
        let settings =
            Settings::<DockerConfig>::new(Some(&settings_path)).expect("Invalid settings file");
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: true,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();

        // this should fail as call to encrypt fails
        check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap_err();
    }

    #[test]
    fn settings_non_x509_auth_change_creates_new_backup() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let settings = Settings::<DockerConfig>::new(Some(Path::new(SETTINGS))).unwrap();
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();

        let settings1 = Settings::<DockerConfig>::new(Some(Path::new(SETTINGS1))).unwrap();
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings1,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();
        let expected = settings1.compute_settings_digest().unwrap();
        let mut written1 = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written1)
            .unwrap();

        assert_eq!(expected, written1);
        assert_ne!(written1, written);

        // no hybrid key and iv should be created since the auth mode is not X.509
        let f = File::open(tmp_dir.path().join("iotedge_hybrid_key"));
        assert!(f.is_err());
        let f = File::open(tmp_dir.path().join("iotedge_hybrid_iv"));
        assert!(f.is_err());
    }

    #[test]
    fn settings_non_x509_auth_deletes_stale_hybrid_key_and_iv() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let iv_path = tmp_dir.path().join("iotedge_hybrid_iv");
        let hybrid_key_path = tmp_dir.path().join("iotedge_hybrid_key");
        File::create(&iv_path)
            .expect("Stale iv file could not be created")
            .write_all(b"jibba")
            .expect("Stale iv file could not be written");
        File::create(&hybrid_key_path)
            .expect("Stale hybrid key file could not be created")
            .write_all(b"jabba")
            .expect("Stale hybrid key file could not be written");
        let settings = Settings::<DockerConfig>::new(Some(Path::new(SETTINGS))).unwrap();
        let config = TestConfig::new("microsoft/test-image".to_string());
        let state = ModuleRuntimeState::default();
        let module: TestModule<Error> =
            TestModule::new("test-module".to_string(), config, Ok(state));
        let runtime = TestRuntime::new(Ok(module));
        let crypto = TestCrypto {
            use_expired_ca: false,
            fail_device_ca_alias: false,
            fail_decrypt: false,
            fail_encrypt: false,
        };
        let mut tokio_runtime = tokio::runtime::Runtime::new().unwrap();
        check_settings_state(
            &tmp_dir.path().to_path_buf(),
            "settings_state",
            &settings,
            &runtime,
            &crypto,
            &mut tokio_runtime,
            "iotedge_hybrid_key",
            "iotedge_hybrid_iv",
        )
        .unwrap();
        let mut written = String::new();
        File::open(tmp_dir.path().join("settings_state"))
            .unwrap()
            .read_to_string(&mut written)
            .unwrap();
        // no hybrid key and iv should be created and any stale files deleted
        // since the auth mode is not X.509
        let f = File::open(tmp_dir.path().join("iotedge_hybrid_key"));
        assert!(f.is_err());
        let f = File::open(tmp_dir.path().join("iotedge_hybrid_iv"));
        assert!(f.is_err());
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
