// Copyright (c) Microsoft. All rights reserved.

use crate::error::Error as EdgedError;

pub(crate) async fn start<M>(
    settings: &impl edgelet_settings::RuntimeSettings,
    runtime: M,
    sender: tokio::sync::mpsc::UnboundedSender<edgelet_core::ShutdownReason>,
    tasks: std::sync::Arc<std::sync::atomic::AtomicUsize>,
) -> Result<tokio::sync::oneshot::Sender<()>, EdgedError>
where
    M: edgelet_core::ModuleRuntime + Clone + Send + Sync + 'static,
{
    let socket = settings.listen().workload_uri();

    let connector = http_common::Connector::new(&socket)
        .map_err(|err| EdgedError::from_err("Invalid management API URL", err))?;

    let service = edgelet_http_mgmt::Service::new(
        &settings.endpoints().aziot_identityd_url(),
        runtime,
        sender,
    )
    .map_err(|err| EdgedError::from_err("Invalid Identity Service URL", err))?;

    let mut incoming = connector
        .incoming()
        .await
        .map_err(|err| EdgedError::from_err("Failed to listen on management socket", err))?;

    let (shutdown_tx, shutdown_rx) = tokio::sync::oneshot::channel::<()>();

    tokio::spawn(async move {
        log::info!("Starting management API...");

        if let Err(err) = incoming.serve(service, shutdown_rx).await {
            log::error!("Failed to serve management socket: {}", err);
        }

        tasks.fetch_sub(1, std::sync::atomic::Ordering::AcqRel);
        log::info!("Management API stopped");
    });

    Ok(shutdown_tx)
}
