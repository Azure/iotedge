// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::doc_markdown, // clippy want the "IoT" of "IoT Hub" in a code fence
    clippy::missing_errors_doc,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::shadow_unrelated,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::type_complexity,
    clippy::use_self,
)]

pub mod app;
mod error;
pub mod logging;
pub mod signal;
pub mod watchdog;
pub mod workload;

pub mod unix;

use futures::sync::mpsc;
use identity_client::IdentityClient;
use std::fs::DirBuilder;
use std::path::Path;
use std::sync::{Arc, Mutex};
use std::time::Duration;
use std::{collections::BTreeMap, fs::OpenOptions, io::Read};

use edgelet_core::{
    crypto::{AZIOT_EDGED_CA_ALIAS, MANIFEST_TRUST_BUNDLE_ALIAS, TRUST_BUNDLE_ALIAS},
    settings::AutoReprovisioningMode,
};
use edgelet_core::{
    Authenticator, MakeModuleRuntime, Module, ModuleRuntime, ModuleRuntimeErrorReason, ModuleSpec,
    RuntimeSettings, WorkloadConfig,
};
use edgelet_http::logging::LoggingService;
use edgelet_http::{HyperExt, API_VERSION};
use edgelet_http_mgmt::ManagementService;
use edgelet_http_workload::WorkloadService;
use edgelet_utils::log_failure;
pub use error::{Error, ErrorKind, InitializeErrorReason};
use failure::{Context, Fail, ResultExt};
use futures::future::Either;
use futures::sync::oneshot::{self, Receiver};
use futures::{future, Future, Stream};
use hyper::server::conn::Http;
use hyper::{Body, Request};
use log::{debug, error, info, Level};
use serde::de::DeserializeOwned;
use serde::Serialize;
use sha2::{Digest, Sha256};

use crate::watchdog::Watchdog;
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

/// This variable holds the host name for the parent edge device. This name is used
/// by the edge agent to connect to parent edge hub for identity and twin operations.
const GATEWAY_HOSTNAME_KEY: &str = "IOTEDGE_GATEWAYHOSTNAME";

/// This variable holds the host name for the edge device. This name is used
/// by the edge agent to provide the edge hub container an alias name in the
/// network so that TLS cert validation works.
const EDGEDEVICE_HOSTNAME_KEY: &str = "EdgeDeviceHostName";

/// This variable holds the IoT Hub device identifier.
const DEVICEID_KEY: &str = "IOTEDGE_DEVICEID";

/// This variable holds the IoT Hub module identifier.
const MODULEID_KEY: &str = "IOTEDGE_MODULEID";

/// This variable holds the URI to use for connecting to the workload endpoint
/// in aziot-edged. This is used by the edge agent to connect to the workload API
/// for its own needs and is also used for volume mounting into module
/// containers when the URI refers to a Unix domain socket.
const WORKLOAD_URI_KEY: &str = "IOTEDGE_WORKLOADURI";

/// This variable holds the URI to use for connecting to the management
/// endpoint in aziot-edged. This is used by the edge agent for managing module
/// lifetimes and module identities.
const MANAGEMENT_URI_KEY: &str = "IOTEDGE_MANAGEMENTURI";

/// This variable holds the authentication scheme that modules are to use when
/// connecting to other server modules (like Edge Hub). The authentication
/// scheme can mean either that we are to use SAS tokens or a TLS client cert.
const AUTHSCHEME_KEY: &str = "IOTEDGE_AUTHSCHEME";

/// This is the key for the edge runtime mode.
const EDGE_RUNTIME_MODE_KEY: &str = "Mode";

/// This is the edge runtime mode - it should be iotedged, when aziot-edged starts edge runtime in single node mode.
const EDGE_RUNTIME_MODE: &str = "iotedged";

/// This is the key for the largest API version that this edgelet supports
const API_VERSION_KEY: &str = "IOTEDGE_APIVERSION";

/// This is the name of the cache subdirectory for settings state
const EDGE_SETTINGS_SUBDIR: &str = "cache";

/// This is the name of the settings backup file
const EDGE_PROVISIONING_STATE_FILENAME: &str = "provisioning_state";

// 2 hours
const AZIOT_EDGE_ID_CERT_MAX_DURATION_SECS: i64 = 2 * 3600;
// 90 days
const AZIOT_EDGE_SERVER_CERT_MAX_DURATION_SECS: i64 = 90 * 24 * 3600;

const STOP_TIME: Duration = Duration::from_secs(30);

/// This is the interval at which to poll Identity Service for device information.
const IS_GET_DEVICE_INFO_RETRY_INTERVAL_SECS: Duration = Duration::from_secs(5);

#[derive(PartialEq)]
enum StartApiReturnStatus {
    Restart,
    Shutdown,
}

#[derive(Clone, serde::Serialize, serde::Deserialize, Debug)]
pub struct ProvisioningResult {
    device_id: String,
    gateway_host_name: String,
    hub_name: String,
}

pub struct Main<M>
where
    M: MakeModuleRuntime,
{
    settings: M::Settings,
}

impl<M> Main<M>
where
    M: MakeModuleRuntime + Send + 'static,
    M::ModuleRuntime: 'static + Authenticator<Request = Request<Body>> + Clone + Send + Sync,
    <<M::ModuleRuntime as ModuleRuntime>::Module as Module>::Config:
        Clone + DeserializeOwned + Serialize + edgelet_core::module::NestedEdgeBodge,
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
        let Main { mut settings } = self;

        let mut tokio_runtime = tokio::runtime::Runtime::new()
            .context(ErrorKind::Initialize(InitializeErrorReason::Tokio))?;

        let cache_subdir_path = Path::new(&settings.homedir()).join(EDGE_SETTINGS_SUBDIR);
        // make sure the cache directory exists
        DirBuilder::new()
            .recursive(true)
            .create(&cache_subdir_path)
            .context(ErrorKind::Initialize(
                InitializeErrorReason::CreateCacheDirectory,
            ))?;

        let runtime = init_runtime::<M>(settings.clone(), &mut tokio_runtime)?;

        // This "do-while" loop runs until a StartApiReturnStatus::Shutdown
        // is received. If the TLS cert needs a restart, we will loop again.
        loop {
            info!("Obtaining edge device provisioning data...");

            let url = settings.endpoints().aziot_identityd_url().clone();
            let client = Arc::new(Mutex::new(identity_client::IdentityClient::new(
                aziot_identity_common_http::ApiVersion::V2020_09_01,
                &url,
            )));

            match settings.auto_reprovisioning_mode() {
                AutoReprovisioningMode::AlwaysOnStartup => {
                    tokio_runtime.block_on(reprovision_device(&client))?
                }
                AutoReprovisioningMode::Dynamic | AutoReprovisioningMode::OnErrorOnly => {}
            }

            let result =
                tokio_runtime.block_on(
                    client
                        .lock()
                        .unwrap()
                        .get_device()
                        .map_err(|err| {
                            Error::from(err.context(ErrorKind::Initialize(
                                InitializeErrorReason::GetDeviceInfo,
                            )))
                        })
                        .and_then(|identity| match identity {
                            aziot_identity_common::Identity::Aziot(spec) => {
                                debug!("{}:{}", spec.hub_name, spec.device_id.0);
                                Ok(ProvisioningResult {
                                    device_id: spec.device_id.0,
                                    gateway_host_name: spec.gateway_host,
                                    hub_name: spec.hub_name,
                                })
                            }
                            aziot_identity_common::Identity::Local(_) => Err(Error::from(
                                ErrorKind::Initialize(InitializeErrorReason::InvalidIdentityType),
                            )),
                        }),
                );

            match result {
                Ok(provisioning_result) => {
                    info!("Finished provisioning edge device.");

                    // Normally aziot-edged will stop all modules when it shuts down. But if it crashed,
                    // modules will continue to run. On Linux systems where aziot-edged is responsible for
                    // creating/binding the socket (e.g., CentOS 7.5, which uses systemd but does not
                    // support systemd socket activation), modules will be left holding stale file
                    // descriptors for the workload and management APIs and calls on these APIs will
                    // begin to fail. Resilient modules should be able to deal with this, but we'll
                    // restart all modules to ensure a clean start.
                    info!("Stopping all modules...");
                    tokio_runtime
                        .block_on(runtime.stop_all(Some(STOP_TIME)))
                        .context(ErrorKind::Initialize(
                            InitializeErrorReason::StopExistingModules,
                        ))?;
                    info!("Finished stopping modules.");

                    // Detect if the device was changed and if the device needs to be reconfigured
                    check_device_reconfigure::<M>(
                        &cache_subdir_path,
                        EDGE_PROVISIONING_STATE_FILENAME,
                        &provisioning_result,
                        &runtime,
                        &mut tokio_runtime,
                    )?;

                    settings
                        .agent_mut()
                        .parent_hostname_resolve(&provisioning_result.gateway_host_name);

                    let cfg = WorkloadData::new(
                        provisioning_result.hub_name,
                        provisioning_result.device_id,
                        settings
                            .edge_ca_cert()
                            .unwrap_or(AZIOT_EDGED_CA_ALIAS)
                            .to_string(),
                        settings
                            .edge_ca_key()
                            .unwrap_or(AZIOT_EDGED_CA_ALIAS)
                            .to_string(),
                        settings
                            .trust_bundle_cert()
                            .unwrap_or(TRUST_BUNDLE_ALIAS)
                            .to_string(),
                        settings
                            .manifest_trust_bundle_cert()
                            .unwrap_or(MANIFEST_TRUST_BUNDLE_ALIAS)
                            .to_string(),
                        AZIOT_EDGE_ID_CERT_MAX_DURATION_SECS,
                        AZIOT_EDGE_SERVER_CERT_MAX_DURATION_SECS,
                    );

                    let (code, should_reprovision) = start_api::<_, _, M>(
                        &settings,
                        &provisioning_result.gateway_host_name,
                        &runtime,
                        cfg.clone(),
                        make_shutdown_signal(),
                        &mut tokio_runtime,
                    )?;

                    if should_reprovision {
                        tokio_runtime.block_on(reprovision_device(&client))?;
                    }

                    if code != StartApiReturnStatus::Restart {
                        break;
                    }
                }
                Err(err) => {
                    log_failure(Level::Warn, &err);

                    std::thread::sleep(IS_GET_DEVICE_INFO_RETRY_INTERVAL_SECS);

                    log::warn!("Retrying getting edge device provisioning information.");
                }
            };
        }

        info!("Shutdown complete.");
        Ok(())
    }
}

fn reprovision_device(
    identity_client: &Arc<Mutex<IdentityClient>>,
) -> impl Future<Item = (), Error = Error> {
    let id_mgr = identity_client.lock().unwrap();
    id_mgr
        .reprovision_device()
        .map_err(|err| Error::from(err.context(ErrorKind::ReprovisionFailure)))
}

fn check_device_reconfigure<M>(
    subdir: &Path,
    filename: &str,
    provisioning_result: &ProvisioningResult,
    runtime: &M::ModuleRuntime,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<(), Error>
where
    M: MakeModuleRuntime + 'static,
{
    info!("Detecting if device information has changed...");
    let path = subdir.join(filename);

    let diff = diff_with_cached(provisioning_result, &path);
    if diff {
        info!("Change to provisioning state detected.");
        reconfigure::<M>(
            subdir,
            filename,
            provisioning_result,
            runtime,
            tokio_runtime,
        )?;
    }

    Ok(())
}

fn compute_provisioning_result_digest(
    provisioning_result: &ProvisioningResult,
) -> Result<String, DiffError> {
    let s = serde_json::to_string(provisioning_result)?;
    Ok(base64::encode(&Sha256::digest_str(&s)))
}

fn diff_with_cached(provisioning_result: &ProvisioningResult, path: &Path) -> bool {
    fn diff_with_cached_inner(
        provisioning_result: &ProvisioningResult,
        path: &Path,
    ) -> Result<bool, DiffError> {
        let mut file = OpenOptions::new().read(true).open(path)?;
        let mut buffer = String::new();
        file.read_to_string(&mut buffer)?;
        let encoded = compute_provisioning_result_digest(provisioning_result)?;
        if encoded == buffer {
            debug!("Provisioning state matches supplied provisioning result.");
            Ok(false)
        } else {
            Ok(true)
        }
    }

    match diff_with_cached_inner(provisioning_result, path) {
        Ok(result) => result,

        Err(err) => {
            log_failure(Level::Debug, &err);
            debug!("Error reading config backup.");
            true
        }
    }
}

#[derive(Debug, Fail)]
#[fail(display = "Could not load provisioning result")]
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

fn reconfigure<M>(
    subdir: &Path,
    filename: &str,
    provisioning_result: &ProvisioningResult,
    runtime: &M::ModuleRuntime,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<(), Error>
where
    M: MakeModuleRuntime + 'static,
{
    info!("Removing all modules...");
    tokio_runtime
        .block_on(runtime.remove_all())
        .context(ErrorKind::Initialize(
            InitializeErrorReason::RemoveExistingModules,
        ))?;
    info!("Finished removing modules.");

    let path = subdir.join(filename);

    // store provisioning result
    let digest = compute_provisioning_result_digest(provisioning_result).context(
        ErrorKind::Initialize(InitializeErrorReason::SaveProvisioning),
    )?;

    std::fs::write(path, digest.into_bytes()).context(ErrorKind::Initialize(
        InitializeErrorReason::SaveProvisioning,
    ))?;

    Ok(())
}

#[allow(clippy::too_many_arguments)]
fn start_api<F, W, M>(
    settings: &M::Settings,
    parent_hostname: &str,
    runtime: &M::ModuleRuntime,
    workload_config: W,
    shutdown_signal: F,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<(StartApiReturnStatus, bool), Error>
where
    F: Future<Item = (), Error = ()> + Send + 'static,
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
    let iot_hub_name = workload_config.iot_hub_name().to_string();
    let device_id = workload_config.device_id().to_string();

    let (mgmt_tx, mgmt_rx) = oneshot::channel();
    let (mgmt_stop_and_reprovision_tx, mgmt_stop_and_reprovision_rx) = mpsc::unbounded();
    let (work_tx, work_rx) = oneshot::channel();

    let mgmt = start_management::<M>(settings, runtime, mgmt_rx, mgmt_stop_and_reprovision_tx);

    let workload = start_workload::<_, M>(settings, runtime, work_rx, workload_config);

    let (runt_tx, runt_rx) = oneshot::channel();
    let edge_rt = start_runtime::<M>(
        runtime.clone(),
        &iot_hub_name,
        parent_hostname,
        &device_id,
        &settings,
        runt_rx,
    )?;

    // This mpsc sender/receiver is used for getting notifications from the mgmt service
    // indicating that the daemon should shut down and attempt to reprovision the device.
    let mgmt_stop_and_reprovision_signaled =
        mgmt_stop_and_reprovision_rx
            .into_future()
            .then(|res| match res {
                Ok((Some(()), _)) | Ok((None, _)) => Ok(()),
                Err(((), _)) => Err(Error::from(ErrorKind::ManagementService)),
            });

    let mgmt_stop_and_reprovision_signaled = match settings.auto_reprovisioning_mode() {
        AutoReprovisioningMode::Dynamic => {
            futures::future::Either::B(mgmt_stop_and_reprovision_signaled)
        }
        AutoReprovisioningMode::AlwaysOnStartup | AutoReprovisioningMode::OnErrorOnly => {
            futures::future::Either::A(future::empty())
        }
    };

    let edge_rt_with_mgmt_signal = edge_rt.select2(mgmt_stop_and_reprovision_signaled).then(
        |res| match res {
            Ok(Either::A((_edge_rt_ok, _mgmt_stop_and_reprovision_signaled_future))) => {
                info!("Edge runtime will stop because of the shutdown signal.");
                future::ok((StartApiReturnStatus::Shutdown, false))
            }
            Ok(Either::B((_mgmt_stop_and_reprovision_signaled_ok, _edge_rt_future))) => {
                info!("Edge runtime will stop because of the device reprovisioning signal.");
                future::ok((StartApiReturnStatus::Shutdown, true))
            }
            Err(Either::A((edge_rt_err, _mgmt_stop_and_reprovision_signaled_future))) => {
                error!("Edge runtime will stop because the shutdown signal raised an error.");
                future::err(edge_rt_err)
            },
            Err(Either::B((mgmt_stop_and_reprovision_signaled_err, _edge_rt_future))) => {
                error!("Edge runtime will stop because the device reprovisioning signal raised an error.");
                future::err(mgmt_stop_and_reprovision_signaled_err)
            }
        },
    );

    // Wait for the watchdog to finish, and then send signal to the workload and management services.
    // This way the edgeAgent can finish shutting down all modules.
    let edge_rt_with_cleanup = edge_rt_with_mgmt_signal.then(move |res| {
        mgmt_tx.send(()).unwrap_or(());
        work_tx.send(()).unwrap_or(());

        // A -> EdgeRt + Mgmt Stop and Reprovision Signal Future
        // B -> Restart Signal Future
        match res {
            Ok((start_api_return_status, should_reprovision)) => {
                future::ok((start_api_return_status, should_reprovision))
            }
            Err(err) => future::err(err),
        }
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
            Ok(((), (), (code, should_reprovision))) => Ok((code, should_reprovision)),
            Err(err) => Err(err),
        });
    let (restart_code, should_reprovision) = tokio_runtime.block_on(services)?;
    Ok((restart_code, should_reprovision))
}

fn init_runtime<M>(
    settings: M::Settings,
    tokio_runtime: &mut tokio::runtime::Runtime,
) -> Result<M::ModuleRuntime, Error>
where
    M: MakeModuleRuntime + Send + 'static,
    M::ModuleRuntime: Send,
    M::Future: 'static,
{
    info!("Initializing the module runtime...");
    let runtime = tokio_runtime
        .block_on(M::make_runtime(settings))
        .context(ErrorKind::Initialize(InitializeErrorReason::ModuleRuntime))?;
    info!("Finished initializing the module runtime.");

    Ok(runtime)
}

fn start_runtime<M>(
    runtime: M::ModuleRuntime,
    hostname: &str,
    parent_hostname: &str,
    device_id: &str,
    settings: &M::Settings,
    shutdown: Receiver<()>,
) -> Result<impl Future<Item = (), Error = Error>, Error>
where
    M: MakeModuleRuntime,
    M::ModuleRuntime: Clone + 'static,
    <<M::ModuleRuntime as ModuleRuntime>::Module as Module>::Config:
        Clone + DeserializeOwned + Serialize,
    <M::ModuleRuntime as ModuleRuntime>::Logs: Into<Body>,
    for<'r> &'r <M::ModuleRuntime as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
{
    let spec = settings.agent().clone();
    let env = build_env(spec.env(), hostname, parent_hostname, device_id, settings);
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
        settings.watchdog().max_retries(),
        settings.endpoints().aziot_identityd_url(),
    );
    let runtime_future = watchdog
        .run_until(spec, EDGE_RUNTIME_MODULEID, shutdown.map_err(|_| ()))
        .map_err(Error::from);

    Ok(runtime_future)
}

// Add the environment variables needed by the EdgeAgent.
fn build_env<S>(
    spec_env: &BTreeMap<String, String>,
    hostname: &str,
    parent_hostname: &str,
    device_id: &str,
    settings: &S,
) -> BTreeMap<String, String>
where
    S: RuntimeSettings,
{
    let mut env = BTreeMap::new();
    env.insert(HOSTNAME_KEY.to_string(), hostname.to_string());
    env.insert(
        EDGEDEVICE_HOSTNAME_KEY.to_string(),
        settings.hostname().to_string().to_lowercase(),
    );

    if parent_hostname.to_lowercase() != hostname.to_lowercase() {
        env.insert(
            GATEWAY_HOSTNAME_KEY.to_string(),
            parent_hostname.to_string(),
        );
    }

    env.insert(DEVICEID_KEY.to_string(), device_id.to_string());
    env.insert(MODULEID_KEY.to_string(), EDGE_RUNTIME_MODULEID.to_string());

    #[cfg(feature = "runtime-docker")]
    let (workload_uri, management_uri) = (
        settings.connect().workload_uri().to_string(),
        settings.connect().management_uri().to_string(),
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

fn start_management<M>(
    settings: &M::Settings,
    runtime: &M::ModuleRuntime,
    shutdown: Receiver<()>,
    initiate_shutdown_and_reprovision: mpsc::UnboundedSender<()>,
) -> impl Future<Item = (), Error = Error>
where
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

    let identity_uri = settings.endpoints().aziot_identityd_url().clone();
    let identity_client = Arc::new(Mutex::new(identity_client::IdentityClient::new(
        aziot_identity_common_http::ApiVersion::V2020_09_01,
        &identity_uri,
    )));

    ManagementService::new(runtime, identity_client, initiate_shutdown_and_reprovision)
        .then(move |service| -> Result<_, Error> {
            let service = service.context(ErrorKind::Initialize(
                InitializeErrorReason::ManagementService,
            ))?;
            let service = LoggingService::new(label, service);

            let run = Http::new()
                .bind_url(url.clone(), service)
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

fn start_workload<W, M>(
    settings: &M::Settings,
    runtime: &M::ModuleRuntime,
    shutdown: Receiver<()>,
    config: W,
) -> impl Future<Item = (), Error = Error>
where
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

    let keyd_url = settings.endpoints().aziot_keyd_url().clone();
    let certd_url = settings.endpoints().aziot_certd_url().clone();
    let identityd_url = settings.endpoints().aziot_identityd_url().clone();

    let key_connector = http_common::Connector::new(&keyd_url).expect("Connector");
    let key_client = Arc::new(aziot_key_client::Client::new(
        aziot_key_common_http::ApiVersion::V2020_09_01,
        key_connector,
    ));

    let cert_client = Arc::new(Mutex::new(cert_client::CertificateClient::new(
        aziot_cert_common_http::ApiVersion::V2020_09_01,
        &certd_url,
    )));
    let identity_client = Arc::new(Mutex::new(identity_client::IdentityClient::new(
        aziot_identity_common_http::ApiVersion::V2020_09_01,
        &identityd_url,
    )));

    WorkloadService::new(runtime, identity_client, cert_client, key_client, config)
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

    use edgelet_docker::Settings;

    use super::{Fail, RuntimeSettings};

    static GOOD_SETTINGS_EDGE_CA_CERT_ID: &str = "test/linux/sample_settings.edge.ca.id.toml";
    #[derive(Clone, Copy, Debug, Fail)]
    pub struct Error;

    impl fmt::Display for Error {
        fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
            write!(f, "Error")
        }
    }

    lazy_static::lazy_static! {
        static ref ENV_LOCK: std::sync::Mutex<()> = Default::default();
    }

    #[test]
    fn settings_for_edge_ca_cert() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");
        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS_EDGE_CA_CERT_ID);
        let settings = Settings::new().unwrap();
        assert_eq!(settings.edge_ca_cert(), Some("iotedge-test-ca"));
    }
}
