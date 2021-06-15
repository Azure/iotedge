use std::{
    collections::HashMap,
    path::PathBuf,
    sync::{Arc, Mutex},
};

use crate::error::{Error, ErrorKind, InitializeErrorReason};
use aziot_key_client::Client;
use cert_client::CertificateClient;
use edgelet_core::{
    Authenticator, Listen, MakeModuleRuntime, Module, ModuleAction, ModuleRuntime,
    ModuleRuntimeErrorReason, RuntimeSettings, WorkloadConfig,
};

use edgelet_http::{logging::LoggingService, HyperExt};
use edgelet_http_workload::WorkloadService;
use edgelet_utils::log_failure;
use failure::{Fail, ResultExt};
use futures::{
    sync::{mpsc::UnboundedReceiver, oneshot, oneshot::Sender},
    Future, Stream,
};
use hyper::{server::conn::Http, Body, Request};
use identity_client::IdentityClient;
use log::{error, info, Level};
use serde::{de::DeserializeOwned, Serialize};
use url::Url;

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
    listen_uri: Listen,
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

        let home_dir = settings.homedir().clone().to_path_buf();
        let listen_uri = settings.listen().clone();

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
            listen_uri,
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

    fn start(
        &mut self,
        module_id: &str,
        signal_socket_created: Option<Sender<()>>,
    ) -> Result<(), Error> {
        let (shutdown_sender, shutdown_receiver) = oneshot::channel();

        let label = "work".to_string();

        self.shutdown_senders
            .insert(module_id.to_string(), shutdown_sender);

        let workload_uri = if module_id.is_empty() {
            self.legacy_workload_uri.clone()
        } else {
            if let Some(home_dir) = self.home_dir.to_str() {
                self.listen_uri
                    .workload_uri(home_dir, module_id)
                    .map_err(|err| {
                        log_failure(Level::Error, &err);
                        Error::from(err.context(ErrorKind::WorkloadManager))
                    })
            } else {
                error!("No home dir found");
                Err(Error::from(ErrorKind::WorkloadManager))
            }?
        };

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
                .bind_url(workload_uri.clone(), service)
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
                .run_until(shutdown_receiver.map_err(|_| ()))
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

    fn stop(&mut self, module_id: &str) -> Result<(), Error> {
        let shutdown_sender = self.shutdown_senders.remove(module_id);

        if let Some(shutdown_sender) = shutdown_sender {
            if shutdown_sender.send(()).is_err() {
                error!("Received message that a module stopped, but was unable to close the socket server");
                Err(Error::from(ErrorKind::WorkloadManager))
            } else {
                Ok(())
            }
        } else {
            error!("Couldn't find a matching module Id in the list of shutdown channels");
            Err(Error::from(ErrorKind::WorkloadManager))
        }
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
    workload_manager.start(&String::new(), None)?;

    module_list
        .iter()
        .try_for_each(|m: &<M as ModuleRuntime>::Module| -> Result<(), Error> {
            workload_manager
                .start(m.name(), None)
                .map_err(|err| Error::from(err.context(ErrorKind::WorkloadService)))
        })?;

    let future = create_socket_channel_rcv.for_each(move |module_id| match module_id {
        ModuleAction::Start(module_id, sender) => workload_manager
            .start(&module_id, Some(sender))
            .map_err(|_| ()),
        ModuleAction::Stop(module_id) => workload_manager.stop(&module_id).map_err(|_| ()),
    });

    tokio::spawn(future);

    Ok(())
}
