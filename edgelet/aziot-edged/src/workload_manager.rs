use std::collections::HashMap;

use edgelet_core::{module::ModuleAction, Error, UrlExt};
use edgelet_settings::uri::Listen;

use crate::error::Error as EdgedError;

const SOCKET_DEFAULT_PERMISSION: u32 = 0o666;

pub(crate) struct WorkloadManager<M>
where
    M: edgelet_core::ModuleRuntime + Clone + Send + Sync + 'static,
    M::Config: serde::Serialize,
{
    shutdown_senders: HashMap<String, tokio::sync::oneshot::Sender<()>>,
    legacy_workload_uri: url::Url,
    legacy_workload_systemd_socket_name: String,
    home_dir: std::path::PathBuf,
    service: edgelet_http_workload::Service<M>,
}

impl<M> WorkloadManager<M>
where
    M: edgelet_core::ModuleRuntime + Clone + Send + Sync + 'static,
    M::Config: serde::Serialize,
{
    pub(crate) async fn start(
        settings: &impl edgelet_settings::RuntimeSettings,
        runtime: M,
        device_info: &aziot_identity_common::AzureIoTSpec,
        tasks: std::sync::Arc<std::sync::atomic::AtomicUsize>,
        create_socket_channel_snd: tokio::sync::mpsc::UnboundedSender<ModuleAction>,
    ) -> Result<(WorkloadManager<M>, tokio::sync::oneshot::Sender<()>), EdgedError> {
        let shutdown_senders: HashMap<String, tokio::sync::oneshot::Sender<()>> = HashMap::new();
        let (shutdown_tx, shutdown_rx) = tokio::sync::oneshot::channel::<()>();
        let module_runtime = runtime.clone();

        let legacy_workload_uri = settings.listen().legacy_workload_uri().clone();
        let legacy_workload_systemd_socket_name = Listen::get_workload_systemd_socket_name();

        let service = edgelet_http_workload::Service::new(settings, runtime, device_info)
            .map_err(|err| EdgedError::from_err("Invalid service endpoint", err))?;

        service.check_edge_ca().await.map_err(EdgedError::new)?;

        let home_dir = settings.homedir().to_path_buf();

        let workload_manager = WorkloadManager {
            shutdown_senders,
            legacy_workload_uri,
            legacy_workload_systemd_socket_name,
            home_dir,
            service,
        };

        tokio::spawn(stop(
            create_socket_channel_snd,
            module_runtime,
            shutdown_rx,
            tasks,
        ));

        Ok((workload_manager, shutdown_tx))
    }

    async fn spawn_listener(
        &mut self,
        workload_uri: url::Url,
        signal_socket_created: Option<tokio::sync::oneshot::Sender<()>>,
        module_id: &str,
        socket_name: Option<String>,
    ) -> Result<(), EdgedError> {
        let (shutdown_sender, shutdown_receiver) = tokio::sync::oneshot::channel();

        self.shutdown_senders
            .insert(module_id.to_string(), shutdown_sender);

        let connector = http_common::Connector::new(&workload_uri)
            .map_err(|err| EdgedError::from_err("Invalid workload API URL", err))?;

        let mut incoming = connector
            .incoming(SOCKET_DEFAULT_PERMISSION, socket_name)
            .await
            .map_err(|err| EdgedError::from_err("Failed to listen on workload socket", err))?;

        // Send signal back to module runtime that socket and folder are created.
        if let Some(signal_socket_created) = signal_socket_created {
            signal_socket_created.send(()).map_err(|()| {
                EdgedError::from_err(
                    "Could not send socket created signal to module runtime",
                    Error::WorkloadManager,
                )
            })?;
        }

        let service = self.service.clone();
        tokio::spawn(async move {
            log::info!("Starting workload API...");

            if let Err(err) = incoming.serve(service, shutdown_receiver).await {
                log::error!("Failed to start workload API: {}", err);
            }

            log::info!("Workload API stopped");
        });

        Ok(())
    }

    async fn start_listener(
        &mut self,
        module_id: &str,
        signal_socket_created: Option<tokio::sync::oneshot::Sender<()>>,
    ) -> Result<(), EdgedError> {
        log::info!("Starting new listener for module {}", module_id);
        let workload_uri = self.get_listener_uri(module_id)?;

        self.spawn_listener(workload_uri, signal_socket_created, module_id, None)
            .await?;

        Ok(())
    }

    fn stop_listener(&mut self, module_id: &str) {
        log::info!("Stopping listener for module {}", module_id);

        let shutdown_sender = self.shutdown_senders.remove(module_id);

        if let Some(shutdown_sender) = shutdown_sender {
            // When edged boots up, it cleans all modules. At this moment, no socket could listening so it could legitimately return an error.
            let _ = shutdown_sender.send(());
        }
    }

    fn remove_listener(&mut self, module_id: &str) -> Result<(), EdgedError> {
        log::info!("Removing listener for module {}", module_id);

        // Try to stop the listener, just in case it was not stopped before
        self.stop_listener(module_id);

        // If the container is removed, also remove the socket file to limit the leaking of socket file
        let workload_uri = self.get_listener_uri(module_id)?;

        let path = workload_uri
            .to_uds_file_path()
            .map_err(|err| EdgedError::from_err("Could not convert uri to path", err))?;

        std::fs::remove_file(path)
            .map_err(|err| EdgedError::from_err("Could not remove socket", err))?;

        Ok(())
    }

    fn get_listener_uri(&self, module_id: &str) -> Result<url::Url, EdgedError> {
        let uri = self.home_dir.to_str().map_or_else(
            || {
                Err(EdgedError::from_err(
                    "No home dir found",
                    Error::WorkloadManager,
                ))
            },
            |home_dir| {
                Listen::workload_uri(home_dir, module_id)
                    .map_err(|err| EdgedError::from_err("Could not get workload uri", err))
            },
        )?;

        Ok(uri)
    }
}

pub(crate) async fn server<M>(
    mut workload_manager: WorkloadManager<M>,
    runtime: M,
    mut create_socket_channel_rcv: tokio::sync::mpsc::UnboundedReceiver<ModuleAction>,
) -> Result<(), EdgedError>
where
    M: edgelet_core::ModuleRuntime + Clone + Send + Sync + 'static,
    M::Config: serde::Serialize,
{
    let module_list = runtime
        .list()
        .await
        .map_err(|err| EdgedError::from_err("Could not list modules", err))?;

    let socket_name = workload_manager.legacy_workload_systemd_socket_name.clone();
    // Spawn a listener for module that are still running and uses old listen socket
    workload_manager
        .spawn_listener(
            workload_manager.legacy_workload_uri.clone(),
            None,
            "",
            Some(socket_name),
        )
        .await?;

    for module in module_list {
        if let Err(err) = workload_manager
            .start_listener(edgelet_core::Module::name(&module), None)
            .await
        {
            log::error!(
                "Could not start listener for module {}, error {}",
                edgelet_core::Module::name(&module),
                err
            );
        }
    }

    // Ignore error, we don't want the server to close on error.
    tokio::spawn(async move {
        loop {
            if let Some(module_id) = create_socket_channel_rcv.recv().await {
                match module_id {
                    ModuleAction::Start(module_id, sender) => {
                        if let Err(err) = workload_manager
                            .start_listener(&module_id, Some(sender))
                            .await
                        {
                            log::info!("Failed to start module {}, error {}", module_id, err);
                        }
                    }
                    ModuleAction::Stop(module_id) => workload_manager.stop_listener(&module_id),
                    ModuleAction::Remove(module_id) => {
                        if let Err(err) = workload_manager.remove_listener(&module_id) {
                            log::info!("Failed to remove module {}, error {}", module_id, err);
                        }
                    }
                }
            }
        }
    });

    Ok(())
}

async fn stop<M>(
    create_socket_channel_snd: tokio::sync::mpsc::UnboundedSender<ModuleAction>,
    runtime: M,
    shutdown_rx: tokio::sync::oneshot::Receiver<()>,
    tasks: std::sync::Arc<std::sync::atomic::AtomicUsize>,
) -> Result<(), EdgedError>
where
    M: edgelet_core::ModuleRuntime + Clone + Send + Sync + 'static,
    M::Config: serde::Serialize,
{
    if let Err(err) = shutdown_rx.await {
        return  Err(EdgedError::from_err("Could wait on the stop signal, workload manager will continue but not shutdown properly", err));
    }

    let module_list = runtime
        .list()
        .await
        .map_err(|err| EdgedError::from_err("Could not list modules", err))?;

    for module in module_list {
        create_socket_channel_snd
            .send(ModuleAction::Stop(
                edgelet_core::Module::name(&module).to_string(),
            ))
            .map_err(|_| {
                log::info!(
                    "Could not notify back runtime, stop listener for module {}",
                    edgelet_core::Module::name(&module)
                );
                EdgedError::from_err(
                    "Could not notify back runtime, stop listener",
                    Error::WorkloadManager,
                )
            })?;
    }

    tasks.fetch_sub(1, std::sync::atomic::Ordering::AcqRel);
    log::info!("Workload Manager stopped");

    Ok(())
}
