mod bootstrap;
mod shutdown;
mod snapshot;

use std::{fs, path::Path};

use anyhow::{Context, Result};
use futures_util::pin_mut;
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
    /*
        create persistor and load state
        get shutdown signal
        start the server
        start the sidecars
        wait on either the server or sidecars to exit, on exit shut everything down and Err if needed
    */

    let config = bootstrap::config(config_path).context(LoadConfigurationError)?;

    info!("loading state...");
    let persistence_config = config.broker().persistence();
    let state_dir = persistence_config.file_path();

    fs::create_dir_all(state_dir.clone())?;
    let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
    let state = persistor.load().await?;
    info!("state loaded.");

    let broker = bootstrap::broker(config.broker(), state).await?;
    let broker_handle = broker.handle();
    let system_address = config.listener().system().addr().to_string();

    let shutdown_signal = shutdown::shutdown();
    pin_mut!(shutdown_signal);

    // start broker
    // TODO REVIEW: need to tokio spawn
    info!("starting server...");
    let state = bootstrap::start_server(config, broker, shutdown_signal)
        .await
        .unwrap();

    // start sidecars
    let (sidecar_shutdown, sidecar_join_handles) =
        bootstrap::start_sidecars(broker_handle, system_address)
            .await
            .unwrap();

    // combine future for all sidecars
    // wait on future for sidecars or broker
    // if one of them exits then shut the other down

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
