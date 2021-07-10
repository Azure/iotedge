// Copyright (c) Microsoft. All rights reserved.

use crate::error::Error as EdgedError;

pub(crate) async fn start(
    settings: &edgelet_docker::Settings,
    tasks: std::sync::Arc<std::sync::atomic::AtomicUsize>,
) -> Result<tokio::sync::oneshot::Sender<()>, EdgedError> {
    // TODO: fix support in http_common for fd://
    let socket = url::Url::parse("unix:///tmp/workload_test.sock").unwrap();

    let connector = http_common::Connector::new(&socket)
        .map_err(|err| EdgedError::from_err("Invalid workload API URL", err))?;

    let service = edgelet_http_workload::Service::new(settings)
        .map_err(|err| EdgedError::from_err("Invalid service endpoint", err))?;

    let mut incoming = connector
        .incoming()
        .await
        .map_err(|err| EdgedError::from_err("Failed to listen on workload socket", err))?;

    let (shutdown_tx, shutdown_rx) = tokio::sync::oneshot::channel::<()>();

    tokio::spawn(async move {
        log::info!("Starting workload API...");

        if let Err(err) = incoming.serve(service, shutdown_rx).await {
            log::error!("Failed to start workload API: {}", err);
        }

        tasks.fetch_sub(1, std::sync::atomic::Ordering::AcqRel);
        log::info!("Workload API stopped");
    });

    Ok(shutdown_tx)
}
