use std::{collections::HashMap, fs, path::PathBuf, sync::Arc};

use failure::{Fail, ResultExt};
use futures::{
    sync::{mpsc::UnboundedReceiver, oneshot, oneshot::Sender},
    Future, Stream,
};
use hyper::{server::conn::Http, Body, Request};
use log::{error, info, warn, Level};
use serde::{de::DeserializeOwned, Serialize};
use url::Url;

use edgelet_core::crypto::{
    CreateCertificate, Decrypt, Encrypt, GetTrustBundle, KeyStore, MasterEncryptionKey,
};
use edgelet_core::{
    Authenticator, Listen, MakeModuleRuntime, Module, ModuleAction, ModuleRuntime,
    ModuleRuntimeErrorReason, Protocol, RuntimeSettings, UrlExt, WorkloadConfig,
};

use edgelet_http::{
    certificate_manager::CertificateManager, logging::LoggingService, ConcurrencyThrottling,
    HyperExt, TlsAcceptorParams,
};
use edgelet_http_workload::WorkloadService;
use edgelet_utils::log_failure;

use crate::error::{Error, ErrorKind, InitializeErrorReason};

const SOCKET_DEFAULT_PERMISSION: u32 = 0o666;
const MAX_CONCURRENCY: ConcurrencyThrottling = ConcurrencyThrottling::Limited(10);
const EDGEAGENT: &str = "edgeAgent";

pub struct WorkloadManager<K, C, W, M>
where
    C: CreateCertificate
        + Decrypt
        + Encrypt
        + GetTrustBundle
        + MasterEncryptionKey
        + Clone
        + Send
        + Sync
        + 'static,
{
    module_runtime: M,
    shutdown_senders: HashMap<String, oneshot::Sender<()>>,
    legacy_workload_uri: Url,
    home_dir: PathBuf,
    key_store: K,
    cert_manager: Arc<CertificateManager<C>>,
    crypto: C,
    config: W,
    min_protocol_version: Protocol,
}

impl<K, C, W, M> WorkloadManager<K, C, W, M>
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
    M: ModuleRuntime + 'static + Authenticator<Request = Request<Body>> + Clone + Send + Sync,
    <<M as Authenticator>::AuthenticateFuture as Future>::Error: Fail,
    for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
    <<M as ModuleRuntime>::Module as Module>::Config: Clone + DeserializeOwned + Serialize,
    <M as ModuleRuntime>::Logs: Into<Body>,
{
    #[allow(clippy::too_many_arguments)]
    pub fn start_manager<F>(
        settings: &F::Settings,
        key_store: &K,
        runtime: &M,
        crypto: &C,
        config: W,
        cert_manager: Arc<CertificateManager<C>>,
        tokio_runtime: &mut tokio::runtime::Runtime,
        create_socket_channel_rcv: UnboundedReceiver<ModuleAction>,
    ) -> Result<(), Error>
    where
        F: MakeModuleRuntime + 'static,
    {
        let shutdown_senders: HashMap<String, oneshot::Sender<()>> = HashMap::new();

        let legacy_workload_uri = settings.listen().legacy_workload_uri().clone();
        let home_dir = settings.homedir().to_path_buf();

        let min_protocol_version = settings.listen().min_tls_version();

        let module_runtime = runtime.clone();
        let key_store = key_store.clone();
        let crypto = crypto.clone();

        let workload_manager = WorkloadManager {
            module_runtime,
            shutdown_senders,
            legacy_workload_uri,
            home_dir,
            key_store,
            cert_manager,
            crypto,
            config,
            min_protocol_version,
        };

        let module_list: Vec<<M as ModuleRuntime>::Module> =
            tokio_runtime.block_on(runtime.list()).map_err(|err| {
                err.context(ErrorKind::Initialize(
                    InitializeErrorReason::WorkloadService,
                ))
            })?;

        tokio_runtime.block_on(futures::future::lazy(move || {
            server(workload_manager, &module_list, create_socket_channel_rcv)
                .map_err(|err| Error::from(err.context(ErrorKind::WorkloadService)))
        }))?;

        Ok(())
    }

    fn spawn_listener(
        &mut self,
        workload_uri: Url,
        signal_socket_created: Option<Sender<()>>,
        module_id: &str,
        concurrency: ConcurrencyThrottling,
    ) -> Result<(), Error> {
        let label = "work".to_string();

        // If a listener has already been created, remove previous listener.
        // This avoid the launch of 2 listeners.
        // We chose to remove instead and create a new one instead of
        // just return and say, one listener has already been created:
        // We chose to remove because a listener could crash without getting removed correctly.
        // That could make the module crash. Then that module would be restarted without ever going to
        // "stop"
        // There is still a chance that 2 concurrent servers are launch with concurrence,
        // But it is extremely unlikely and anyway doesn't have any side effect expect memory footprint.

        match module_id {
            // If edgeAgent's listener exists, then we do not create a new one
            EDGEAGENT => {
                if self.shutdown_senders.contains_key(EDGEAGENT) {
                    info!(
                        "Listener {} already started, keeping old listener and socket",
                        module_id
                    );
                    if let Some(signal_socket_created) = signal_socket_created {
                        signal_socket_created.send(()).map_err(|()| {
                            ErrorKind::Initialize(InitializeErrorReason::WorkloadService)
                        })?;
                    }
                    return Ok(());
                }
            }
            _ => {
                if let Some(shutdown_sender) = self.shutdown_senders.remove(module_id) {
                    info!(
                        "Listener  {} already started, removing old listener",
                        module_id
                    );
                    shutdown_sender
                        .send(())
                        .map_err(|()| Error::from(ErrorKind::WorkloadService))?;
                }
            }
        }

        let (shutdown_sender, shutdown_receiver) = oneshot::channel();

        self.shutdown_senders
            .insert(module_id.to_string(), shutdown_sender);

        let cert_manager = self.cert_manager.clone();
        let min_protocol_version = self.min_protocol_version;

        let future = WorkloadService::new(
            &self.key_store,
            self.crypto.clone(),
            &self.module_runtime,
            self.config.clone(),
        )
        .then(move |service| -> Result<_, Error> {
            let service = service.context(ErrorKind::Initialize(
                InitializeErrorReason::WorkloadService,
            ))?;
            let service = LoggingService::new(label, service);

            let tls_params = TlsAcceptorParams::new(&cert_manager, min_protocol_version);

            let run = Http::new()
                .bind_url(
                    workload_uri.clone(),
                    service,
                    Some(tls_params),
                    SOCKET_DEFAULT_PERMISSION,
                )
                .map_err(|err| {
                    err.context(ErrorKind::Initialize(
                        InitializeErrorReason::WorkloadService,
                    ))
                })?;

            // Send signal back to module runtime that socket and folder are created.
            if let Some(signal_socket_created) = signal_socket_created {
                signal_socket_created
                    .send(())
                    .map_err(|()| ErrorKind::Initialize(InitializeErrorReason::WorkloadService))?;
            }

            let run = run
                .run_until(shutdown_receiver.map_err(|_| ()), concurrency)
                .map_err(|err| {
                    error!("Closing listener, error {}", err);
                    Error::from(err.context(ErrorKind::WorkloadService))
                });
            info!(
                "Listening on {} with 1 thread for workload API.",
                workload_uri
            );
            Ok(run)
        })
        .flatten()
        .map_err(|_| ());

        tokio::spawn(future);

        Ok(())
    }

    fn get_listener_uri(&self, module_id: &str) -> Result<Url, Error> {
        let uri = if let Some(home_dir) = self.home_dir.to_str() {
            Listen::workload_uri(home_dir, module_id).map_err(|err| {
                log_failure(Level::Error, &err);
                Error::from(err.context(ErrorKind::WorkloadManager))
            })
        } else {
            error!("No home dir found");
            Err(Error::from(ErrorKind::WorkloadManager))
        }?;

        Ok(uri)
    }

    fn start(
        &mut self,
        module_id: &str,
        signal_socket_created: Option<Sender<()>>,
    ) -> Result<(), Error> {
        info!("Starting new listener for module {}", module_id);
        let workload_uri = self.get_listener_uri(module_id)?;

        self.spawn_listener(
            workload_uri,
            signal_socket_created,
            module_id,
            MAX_CONCURRENCY,
        )
    }

    fn stop(&mut self, module_id: &str) -> Result<(), Error> {
        info!("Stopping listener for module {}", module_id);

        // If the container is removed, also remove the socket file to limit the leaking of socket file
        let workload_uri = self.get_listener_uri(module_id)?;

        let path = workload_uri.to_uds_file_path().map_err(|_| {
            warn!("Could not convert uri {} to path", workload_uri);
            ErrorKind::WorkloadManager
        })?;

        fs::remove_file(path).with_context(|_| {
            warn!("Could not remove socket with uri {}", workload_uri);
            ErrorKind::WorkloadManager
        })?;

        // Try to stop the listener, in case it was not stopped before.
        self.stop(module_id)?;

        Ok(())
    }
}

fn server<K, C, W, M>(
    mut workload_manager: WorkloadManager<K, C, W, M>,
    module_list: &[<M as ModuleRuntime>::Module],
    create_socket_channel_rcv: UnboundedReceiver<ModuleAction>,
) -> Result<(), Error>
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
    M: ModuleRuntime + 'static + Authenticator<Request = Request<Body>> + Clone + Send + Sync,
    <<M as Authenticator>::AuthenticateFuture as Future>::Error: Fail,
    for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
    <<M as ModuleRuntime>::Module as Module>::Config: Clone + DeserializeOwned + Serialize,
    <M as ModuleRuntime>::Logs: Into<Body>,
{
    // Spawn a listener for module that are still running and uses old listen socket
    workload_manager.spawn_listener(
        workload_manager.legacy_workload_uri.clone(),
        None,
        "",
        ConcurrencyThrottling::NoLimit,
    )?;

    // Spawn listeners for all module that are still running
    module_list
        .iter()
        .try_for_each(|m: &<M as ModuleRuntime>::Module| -> Result<(), Error> {
            workload_manager
                .start(m.name(), None)
                .map_err(|err| Error::from(err.context(ErrorKind::WorkloadService)))
        })?;

    // Ignore error, we don't want the server to close on error.
    // We do not stop edgeAgent's socket, as we want it to persist
    let server = create_socket_channel_rcv.for_each(move |module_id| match module_id {
        ModuleAction::Start(module_id, sender) => {
            if let Err(err) = workload_manager.start(&module_id, Some(sender)) {
                log_failure(Level::Warn, &err);
            }

            Ok(())
        }
        ModuleAction::Stop(module_id) => {
            if let EDGEAGENT = module_id.as_ref() {
                Ok(())
            } else {
                if let Err(err) = workload_manager.stop(&module_id) {
                    log_failure(Level::Warn, &err);
                }

                Ok(())
            }
        }
    });

    tokio::spawn(server);

    Ok(())
}
