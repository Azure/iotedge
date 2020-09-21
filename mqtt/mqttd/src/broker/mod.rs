mod bootstrap;
mod shutdown;
mod snapshot;

use std::{fs, path::Path};

use anyhow::{Context, Result};
use futures_util::future::select;
use futures_util::future::select_all;
use futures_util::future::Either;
use tokio::{
    task::JoinHandle,
    time::{Duration, Instant},
};
use tracing::{info, warn};

use mqtt_broker::{
    BrokerHandle, FilePersistor, Message, Persist, ShutdownHandle, Snapshotter,
    StateSnapshotHandle, SystemEvent, VersionedFileFormat,
};

// TODO REVIEW: How to shut the broker down?
//              Need to poke around in broker shutdown logic.
pub async fn run<P>(config_path: Option<P>) -> Result<()>
where
    P: AsRef<Path>,
{
    let config = bootstrap::config(config_path).context(LoadConfigurationError)?;

    info!("loading state...");
    let persistence_config = config.broker().persistence();
    let state_dir = persistence_config.file_path();

    fs::create_dir_all(state_dir.clone())?;
    let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
    let state = persistor.load().await?;
    info!("state loaded.");

    let broker = bootstrap::broker(config.broker(), state).await?;
    let mut broker_handle = broker.handle();
    let system_address = config.listener().system().addr().to_string();

    let shutdown_signal = shutdown::shutdown();

    // start broker
    info!("starting server...");
    let server_join_handle =
        tokio::spawn(async move { bootstrap::start_server(config, broker, shutdown_signal).await });

    // start sidecars
    let (sidecar_shutdown, sidecar_join_handles) =
        bootstrap::start_sidecars(broker_handle.clone(), system_address)
            .await
            .unwrap();

    // combine future for all sidecars
    // wait on future for sidecars or broker
    // if one of them exits then shut the other down
    let sidecars_fut = select_all(sidecar_join_handles);
    let state = match select(server_join_handle, sidecars_fut).await {
        Either::Left((server_join_handle, sidecar_join_handle)) => {
            let state = server_join_handle??;

            sidecar_shutdown.shutdown().await?;
            let (completed_join_handle, _, other_handles) = sidecar_join_handle.await;
            completed_join_handle?;
            for handle in other_handles {
                handle.await?;
            }

            state
        }
        Either::Right((sidecar_join_handle, server_join_handle)) => {
            let (completed_join_handle, _, other_handles) = sidecar_join_handle;
            completed_join_handle?;
            for handle in other_handles {
                handle.await?;
            }

            sidecar_shutdown.shutdown().await?;
            broker_handle.send(Message::System(SystemEvent::Shutdown))?;
            let state = server_join_handle.await??;

            state
        }
    };

    info!("persisting state before exiting...");
    persistor.store(state).await?;
    info!("state persisted.");
    info!("exiting... goodbye");

    Ok(())
}

// if let Err(e) = command_handler_shutdown_handle.shutdown().await {
//     error!(message = "failed shutting down command handler", error = %e);
// }
// if let Err(e) = command_handler_join_handle.await {
//     error!(message = "failed waiting for command handler shutdown", error = %e);
// }

async fn start_snapshotter(
    broker_handle: BrokerHandle,
    persistor: FilePersistor<VersionedFileFormat>,
    snapshot_interval: Duration,
) -> (
    ShutdownHandle,
    JoinHandle<FilePersistor<VersionedFileFormat>>,
) {
    let snapshotter = Snapshotter::new(persistor);
    let snapshot_handle = snapshotter.snapshot_handle();
    let shutdown_handle = snapshotter.shutdown_handle();
    let join_handle = tokio::spawn(snapshotter.run());

    // Tick the snapshotter
    let tick = tick_snapshot(
        snapshot_interval,
        broker_handle.clone(),
        snapshot_handle.clone(),
    );
    tokio::spawn(tick);

    // Signal the snapshotter
    let snapshot = snapshot::snapshot(broker_handle, snapshot_handle);
    tokio::spawn(snapshot);

    (shutdown_handle, join_handle)
}

async fn tick_snapshot(
    period: Duration,
    mut broker_handle: BrokerHandle,
    snapshot_handle: StateSnapshotHandle,
) {
    info!("Persisting state every {:?}", period);
    let start = Instant::now() + period;
    let mut interval = tokio::time::interval_at(start, period);
    loop {
        interval.tick().await;
        if let Err(e) = broker_handle.send(Message::System(SystemEvent::StateSnapshot(
            snapshot_handle.clone(),
        ))) {
            warn!(message = "failed to tick the snapshotter", error=%e);
        }
    }
}

#[derive(Debug, thiserror::Error)]
#[error("An error occurred loading configuration.")]
pub struct LoadConfigurationError;
