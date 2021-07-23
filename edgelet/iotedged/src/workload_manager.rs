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

        let (shutdown_sender, shutdown_receiver) = oneshot::channel();

        let cert_manager = self.cert_manager.clone();
        let min_protocol_version = self.min_protocol_version;

        self.shutdown_senders
            .insert(module_id.to_string(), shutdown_sender);

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
        info!("String new listener for module {}", module_id);
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

        let shutdown_sender = self.shutdown_senders.remove(module_id);

        if let Some(shutdown_sender) = shutdown_sender {
            if shutdown_sender.send(()).is_err() {
                warn!("Received message that a module stopped, but was unable to close the socket server");
                Err(Error::from(ErrorKind::WorkloadManager))
            } else {
                Ok(())
            }
        } else {
            warn!("Couldn't find a matching module Id in the list of shutdown channels");
            Err(Error::from(ErrorKind::WorkloadManager))
        }
    }

    fn remove(&mut self, module_id: &str) -> Result<(), Error> {
        info!("Removing listener for module {}", module_id);

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
    let server = create_socket_channel_rcv.for_each(move |module_id| match module_id {
        ModuleAction::Start(module_id, sender) => {
            workload_manager
                .start(&module_id, Some(sender))
                .unwrap_or(());
            Ok(())
        }
        ModuleAction::Stop(module_id) => {
            workload_manager.stop(&module_id).unwrap_or(());
            Ok(())
        }
        ModuleAction::Remove(module_id) => {
            workload_manager.remove(&module_id).unwrap_or(());
            Ok(())
        }
    });

    tokio::spawn(server);

    Ok(())
}
