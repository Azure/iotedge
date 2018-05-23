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
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate tokio_core;
extern crate tokio_signal;
extern crate url;
extern crate url_serde;

pub mod app;
mod error;
pub mod logging;
pub mod settings;
pub mod signal;

use docker::models::HostConfig;
use edgelet_core::crypto::{DerivedKeyStore, KeyStore, MemoryKey, MemoryKeyStore, Sign};
use edgelet_core::watchdog::Watchdog;
use edgelet_core::{ModuleRuntime, ModuleSpec};
use edgelet_docker::{DockerConfig, DockerModuleRuntime};
use edgelet_hsm::Crypto;
use edgelet_hsm::tpm::{TpmKey, TpmKeyStore};
use edgelet_http::client::Client as HttpClient;
use edgelet_http::logging::LoggingService;
use edgelet_http::{ApiVersionService, HyperExt, Run};
use edgelet_http_mgmt::ManagementService;
use edgelet_http_workload::WorkloadService;
use edgelet_iothub::{HubIdentityManager, SasTokenSource};
use futures::Future;
use futures::sync::oneshot::{self, Receiver};
use hsm::ManageTpmKeys;
use hsm::tpm::Tpm;
use hyper::client::Service;
use hyper::server::Http;
use hyper::{Client as HyperClient, Error as HyperError, Request, Response};
use hyper_tls::HttpsConnector;
use iothubservice::DeviceClient;
use provisioning::provisioning::{DpsProvisioning, ManualProvisioning, Provision,
                                 ProvisioningResult};
use std::collections::HashMap;
use tokio_core::reactor::{Core, Handle};
use url::Url;

use settings::{Provisioning, Settings};

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

const IOTHUB_API_VERSION: &str = "2017-11-08-preview";
const DNS_WORKER_THREADS: usize = 4;
const UNIX_SCHEME: &str = "unix";

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

        let runtime = DockerModuleRuntime::new(settings.docker_uri(), &handle)?
            .with_network_id("azure-iot-edge".to_string());

        init_docker_runtime(&runtime, &mut core)?;

        let provisioning_settings = settings.provisioning();
        match *provisioning_settings {
            Provisioning::Manual { .. } => {
                let (key_store, provisioning_result, root_key) =
                    manual_provision(provisioning_settings, &mut core)?;
                start_api(
                    &settings,
                    core,
                    hyper_client,
                    &runtime,
                    &key_store,
                    &provisioning_result.hub_name,
                    &provisioning_result.device_id,
                    root_key,
                    shutdown_signal,
                )?;
            }
            Provisioning::Dps { .. } => {
                let (key_store, provisioning_result, root_key) =
                    dps_provision(provisioning_settings, hyper_client.clone(), &mut core)?;
                start_api(
                    &settings,
                    core,
                    hyper_client,
                    &runtime,
                    &key_store,
                    &provisioning_result.hub_name,
                    &provisioning_result.device_id,
                    root_key,
                    shutdown_signal,
                )?;
            }
        };

        info!("Shutdown complete");
        Ok(())
    }
}

#[cfg_attr(feature = "cargo-clippy", allow(too_many_arguments))]
fn start_api<S, K, F>(
    settings: &Settings<DockerConfig>,
    mut core: Core,
    hyper_client: S,
    runtime: &DockerModuleRuntime,
    key_store: &DerivedKeyStore<K>,
    hub_name: &str,
    device_id: &str,
    root_key: K,
    shutdown_signal: F,
) -> Result<(), Error>
where
    F: Future<Item = (), Error = ()> + 'static,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
    K: 'static + Sign + Clone,
{
    let hostname = format!("https://{}", hub_name);
    let http_client = HttpClient::new(
        hyper_client,
        Some(SasTokenSource::new(
            hub_name.to_string(),
            device_id.to_string(),
            root_key,
        )),
        IOTHUB_API_VERSION,
        Url::parse(&hostname)?,
    )?;
    let device_client = DeviceClient::new(http_client, &device_id)?;
    let id_man = HubIdentityManager::new(key_store.clone(), device_client);

    let (mgmt_tx, mgmt_rx) = oneshot::channel();
    let (work_tx, work_rx) = oneshot::channel();

    let mgmt = start_management(&settings, &core.handle(), &runtime, &id_man, mgmt_rx)?;

    let workload = start_workload(&settings, key_store, &core.handle(), work_rx)?;

    start_runtime(
        &runtime,
        &id_man,
        &mut core,
        &hub_name,
        &device_id,
        &settings,
    )?;

    let shutdown = shutdown_signal.map(move |_| {
        debug!("shutdown signaled");
        mgmt_tx.send(()).unwrap_or(());
        work_tx.send(()).unwrap_or(());
    });

    core.handle().spawn(shutdown);

    core.run(mgmt.join(workload))?;

    Ok(())
}

fn init_docker_runtime(runtime: &DockerModuleRuntime, core: &mut Core) -> Result<(), Error> {
    core.run(runtime.init())?;
    Ok(())
}

fn manual_provision(
    provisioning: &Provisioning,
    core: &mut Core,
) -> Result<(DerivedKeyStore<MemoryKey>, ProvisioningResult, MemoryKey), Error> {
    match *provisioning {
        Provisioning::Manual {
            ref device_connection_string,
        } => {
            let manual = ManualProvisioning::new(device_connection_string.as_str())?;
            let memory_hsm = MemoryKeyStore::new();
            let provision = manual
                .provision(memory_hsm.clone())
                .map_err(Error::from)
                .and_then(move |prov_result| {
                    memory_hsm
                        .get("device", "primary")
                        .map_err(Error::from)
                        .and_then(|k| {
                            let derived_key_store = DerivedKeyStore::new(k.clone());
                            Ok((derived_key_store, prov_result, k))
                        })
                });
            core.run(provision)
        }
        _ => unimplemented!(),
    }
}

fn dps_provision<S>(
    provisioning: &Provisioning,
    hyper_client: S,
    core: &mut Core,
) -> Result<(DerivedKeyStore<TpmKey>, ProvisioningResult, TpmKey), Error>
where
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    match *provisioning {
        Provisioning::Dps {
            ref global_endpoint,
            ref scope_id,
            ref registration_id,
        } => {
            let tpm = Tpm::new().map_err(Error::from)?;
            let ek_result = tpm.get_ek().map_err(Error::from)?;
            let srk_result = tpm.get_srk().map_err(Error::from)?;
            let dps = DpsProvisioning::new(
                hyper_client,
                Url::parse(global_endpoint).expect("Failure creating url"),
                scope_id.to_string(),
                registration_id.to_string(),
                "2017-11-15",
                ek_result,
                srk_result,
            )?;
            let tpm_hsm = TpmKeyStore::new(tpm)?;
            let provision = dps.provision(tpm_hsm.clone())
                .map_err(Error::from)
                .and_then(move |prov_result| {
                    tpm_hsm
                        .get("device", "identity")
                        .map_err(Error::from)
                        .and_then(|k| {
                            let derived_key_store = DerivedKeyStore::new(k.clone());
                            Ok((derived_key_store, prov_result, k))
                        })
                });
            core.run(provision)
        }
        _ => unimplemented!(),
    }
}

fn start_runtime<K, S>(
    runtime: &DockerModuleRuntime,
    id_man: &HubIdentityManager<DerivedKeyStore<K>, S, K>,
    core: &mut Core,
    hostname: &str,
    device_id: &str,
    settings: &Settings<DockerConfig>,
) -> Result<(), Error>
where
    K: 'static + Sign + Clone,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    let spec = settings.runtime().clone();
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

    let mut watchdog = Watchdog::new(runtime.clone(), id_man.clone());
    let runtime_future = watchdog.start(spec, EDGE_RUNTIME_MODULEID);
    // TODO: When this is converted to a watchdog that keeps running, convert this to use a handle
    // that allows it to shutdown gracefully.
    core.run(runtime_future)?;
    Ok(())
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
        settings.hostname().to_string(),
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

    for (key, val) in spec_env.iter() {
        env.insert(key.clone(), val.clone());
    }
    env
}

fn start_management<K, S>(
    settings: &Settings<DockerConfig>,
    handle: &Handle,
    mgmt: &DockerModuleRuntime,
    id_man: &HubIdentityManager<DerivedKeyStore<K>, S, K>,
    shutdown: Receiver<()>,
) -> Result<Run, Error>
where
    K: 'static + Sign + Clone,
    S: 'static + Service<Error = HyperError, Request = Request, Response = Response>,
{
    let url = settings.listen().management_uri().clone();
    let server_handle = handle.clone();
    let service = LoggingService::new(ApiVersionService::new(ManagementService::new(
        mgmt,
        id_man,
    )?));

    info!("Listening on {} with 1 thread for management API.", url);

    let run = Http::new()
        .bind_handle(url, server_handle, service)?
        .run_until(shutdown.map_err(|_| ()));
    Ok(run)
}

fn start_workload<K>(
    settings: &Settings<DockerConfig>,
    key_store: &K,
    handle: &Handle,
    shutdown: Receiver<()>,
) -> Result<Run, Error>
where
    K: 'static + KeyStore + Clone,
{
    let url = settings.listen().workload_uri().clone();
    let server_handle = handle.clone();
    let service = LoggingService::new(ApiVersionService::new(WorkloadService::new(
        key_store,
        Crypto::default(),
    )?));

    info!("Listening on {} with 1 thread for workload API.", url);

    let run = Http::new()
        .bind_handle(url, server_handle, service)?
        .run_until(shutdown.map_err(|_| ()));
    Ok(run)
}
