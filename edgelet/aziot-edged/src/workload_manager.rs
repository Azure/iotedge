use std::{
    collections::HashMap,
    fs,
    path::PathBuf,
    sync::{Arc, Mutex},
};

use failure::{Fail, ResultExt};
use futures::{
    sync::{mpsc::UnboundedReceiver, oneshot, oneshot::Sender},
    Future, Stream,
};
use hyper::{server::conn::Http, Body, Request};
use log::{error, info, warn, Level};
use serde::{de::DeserializeOwned, Serialize};
use url::Url;

use aziot_key_client::Client;
use cert_client::CertificateClient;
use edgelet_core::{
    Authenticator, Listen, MakeModuleRuntime, Module, ModuleAction, ModuleRuntime,
    ModuleRuntimeErrorReason, RuntimeSettings, UrlExt, WorkloadConfig,
};
use edgelet_http::{logging::LoggingService, ConcurrencyThrottling, HyperExt};
use edgelet_http_workload::WorkloadService;
use edgelet_utils::log_failure;
use identity_client::IdentityClient;

use crate::error::{Error, ErrorKind, InitializeErrorReason};

const SOCKET_DEFAULT_PERMISSION: u32 = 0o666;
const MAX_CONCURRENCY: ConcurrencyThrottling = ConcurrencyThrottling::Limited(10);

pub struct WorkloadManager<M, W>
where
    M: ModuleRuntime + 'static + Authenticator<Request = Request<Body>> + Clone + Send + Sync,
    for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
{
    module_runtime: M,
    shutdown_senders: HashMap<String, oneshot::Sender<()>>,
    legacy_workload_uri: Url,
    home_dir: PathBuf,
    key_client: Arc<Client>,
    cert_client: Arc<Mutex<CertificateClient>>,
    identity_client: Arc<Mutex<IdentityClient>>,
    config: W,
}

impl<M, W> WorkloadManager<M, W>
where
    W: WorkloadConfig + Clone + Send + Sync + 'static,
    M: ModuleRuntime + 'static + Authenticator<Request = Request<Body>> + Clone + Send + Sync,
    <<M as Authenticator>::AuthenticateFuture as Future>::Error: Fail,
    for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
    <<M as ModuleRuntime>::Module as Module>::Config: Clone + DeserializeOwned + Serialize,
    <M as ModuleRuntime>::Logs: Into<Body>,
{
    pub fn start_manager<F>(
        settings: &F::Settings,
        runtime: &M,
        config: W,
        tokio_runtime: &mut tokio::runtime::Runtime,
        create_socket_channel_rcv: UnboundedReceiver<ModuleAction>,
    ) -> Result<(), Error>
    where
        F: MakeModuleRuntime + 'static,
    {
        let shutdown_senders: HashMap<String, oneshot::Sender<()>> = HashMap::new();

        let legacy_workload_uri = settings.listen().legacy_workload_uri().clone();
        let keyd_url = settings.endpoints().aziot_keyd_url().clone();
        let certd_url = settings.endpoints().aziot_certd_url().clone();
        let identityd_url = settings.endpoints().aziot_identityd_url().clone();

        let home_dir = settings.homedir().to_path_buf();

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

        let module_runtime = runtime.clone();

        let workload_manager = WorkloadManager {
            module_runtime,
            shutdown_senders,
            legacy_workload_uri,
            home_dir,
            key_client,
            cert_client,
            identity_client,
            config,
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

        self.shutdown_senders
            .insert(module_id.to_string(), shutdown_sender);

        let future = WorkloadService::new(
            &self.module_runtime,
            self.identity_client.clone(),
            self.cert_client.clone(),
            self.key_client.clone(),
            self.config.clone(),
        )
        .then(move |service| -> Result<_, Error> {
            let service = service.context(ErrorKind::Initialize(
                InitializeErrorReason::WorkloadService,
            ))?;
            let service = LoggingService::new(label, service);

            let run = Http::new()
                .bind_url(workload_uri.clone(), service, SOCKET_DEFAULT_PERMISSION)
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
                .map_err(|err| Error::from(err.context(ErrorKind::WorkloadService)));
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

        let shutdown_sender = self.shutdown_senders.remove(module_id);

        if let Some(shutdown_sender) = shutdown_sender {
            if shutdown_sender.send(()).is_err() {
                warn!("Received message that a module stopped, but was unable to close the socket server");
                Err(Error::from(ErrorKind::WorkloadManager))
            } else {
                Ok(())
            }
        } else {
            Ok(())
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

        // Try to stop the listener, in case it was not stopped before.
        self.stop(module_id)?;

        Ok(())
    }
}

fn server<M, W>(
    mut workload_manager: WorkloadManager<M, W>,
    module_list: &[<M as ModuleRuntime>::Module],
    create_socket_channel_rcv: UnboundedReceiver<ModuleAction>,
) -> Result<(), Error>
where
    W: WorkloadConfig + Clone + Send + Sync + 'static,
    M: ModuleRuntime + 'static + Authenticator<Request = Request<Body>> + Clone + Send + Sync,
    <<M as Authenticator>::AuthenticateFuture as Future>::Error: Fail,
    for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
    <<M as ModuleRuntime>::Module as Module>::Config: Clone + DeserializeOwned + Serialize,
    <M as ModuleRuntime>::Logs: Into<Body>,
{
    // Spawn a listener for module that are still running and uses old listen socket
    // Several modules can listen on this socket, so we don't put any limit to the concurrency
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
            if let Err(err) = workload_manager.start(&module_id, Some(sender)) {
                log_failure(Level::Warn, &err);
            }

            Ok(())
        }
        ModuleAction::Stop(module_id) => {
            if let Err(err) = workload_manager.stop(&module_id) {
                log_failure(Level::Warn, &err);
            }

            Ok(())
        }
        ModuleAction::Remove(module_id) => {
            if let Err(err) = workload_manager.remove(&module_id) {
                log_failure(Level::Warn, &err);
            }

            Ok(())
        }
    });

    tokio::spawn(server);

    Ok(())
}
