mod bootstrap;
mod shutdown;
mod snapshot;

use std::{env, fs, path::Path};

use anyhow::{Context, Result};
use futures_util::{
    future::{select, Either},
    pin_mut,
};
use tokio::{
    task::JoinHandle,
    time::{Duration, Instant},
};
use tracing::{error, info, warn};

use mqtt_bridge::BridgeController;
use mqtt_broker::{
    BrokerHandle, FilePersistor, Message, Persist, ShutdownHandle, Snapshotter,
    StateSnapshotHandle, SystemEvent, VersionedFileFormat,
};

const DEVICE_ID_ENV: &str = "IOTEDGE_DEVICEID";

pub async fn run<P>(config_path: Option<P>) -> Result<()>
where
    P: AsRef<Path>,
{
    let config = bootstrap::config(config_path).context(LoadConfigurationError)?;
    let system_address = config.listener().system().addr().to_string();

    info!("loading state...");
    let persistence_config = config.broker().persistence();
    let state_dir = persistence_config.file_path();

    fs::create_dir_all(state_dir.clone())?;
    let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
    let state = persistor.load().await?;
    info!("state loaded.");

    let broker = bootstrap::broker(config.broker(), state).await?;

    info!("starting snapshotter...");
    let snapshot_interval = persistence_config.time_interval();
    let (mut snapshotter_shutdown_handle, snapshotter_join_handle) =
        start_snapshotter(broker.handle(), persistor, snapshot_interval).await;

    let shutdown = shutdown::shutdown();
    pin_mut!(shutdown);

    info!("starting server...");
    let server_fut = bootstrap::start_server(config, broker, shutdown);

    info!("starting bridge controller...");
    let device_id = env::var(DEVICE_ID_ENV)?;
    let mut bridge_controller = BridgeController::new();
    let bridge_controller_fut = bridge_controller.start(system_address, device_id.as_str());

    // TODO PRE: merge conflict resolution with sidecar startup
    pin_mut!(bridge_controller_fut);
    pin_mut!(server_fut);
    let state = match select(server_fut, bridge_controller_fut).await {
        Either::Left((server_output, bridge_fut)) => {
            if let Err(e) = bridge_fut.await {
                error!(message = "bridge failed to exit gracefully", err = %e);
            }

            let state = server_output?;
            state
        }
        Either::Right((bridge_output, server_fut)) => {
            if let Err(e) = bridge_output {
                error!(message = "bridge failed to exit gracefully", err = %e);
            }

            let state = server_fut.await?;
            state
        }
    };

    snapshotter_shutdown_handle.shutdown().await?;
    let mut persistor = snapshotter_join_handle.await?;
    info!("state snapshotter shutdown.");

    info!("persisting state before exiting...");
    persistor.store(state).await?;
    info!("state persisted.");
    info!("exiting... goodbye");

    Ok(())
}

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
