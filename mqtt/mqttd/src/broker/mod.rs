mod bootstrap;
mod shutdown;
mod snapshot;

use std::path::Path;

use anyhow::{Context, Result};
use futures_util::pin_mut;
use std::fs;
use tokio::{
    task::JoinHandle,
    time::{Duration, Instant},
};
use tracing::{info, warn};

use mqtt_broker::{
    BrokerHandle, BrokerSnapshot, Error, FilePersistor, Message, Persist, ShutdownHandle,
    Snapshotter, StateSnapshotHandle, SystemEvent, VersionedFileFormat,
};

pub async fn run<P>(config_path: Option<P>) -> Result<()>
where
    P: AsRef<Path>,
{
    // TODO: apply settings
    // time interval
    let config = bootstrap::config(config_path).context(LoadConfigurationError)?;

    info!("loading state...");
    let state_dir = config.broker().persistence().file_path();
    let (persistor, state) = init_broker_state(state_dir).await?;

    let broker = bootstrap::broker(config.broker(), state).await?;

    info!("starting snapshotter...");
    let (mut shutdown_handle, join_handle) = start_snapshotter(broker.handle(), persistor).await;

    let shutdown = shutdown::shutdown();
    pin_mut!(shutdown);

    info!("starting server...");
    let state = bootstrap::start_server(config, broker, shutdown).await?;

    shutdown_handle.shutdown().await?;
    let mut persistor = join_handle.await?;
    info!("state snapshotter shutdown.");

    info!("persisting state before exiting...");
    persistor.store(state).await?;
    info!("state persisted.");
    info!("exiting... goodbye");

    Ok(())
}

async fn init_broker_state(
    state_dir: String,
) -> Result<(FilePersistor<VersionedFileFormat>, Option<BrokerSnapshot>), Error> {
    let err_message = format!("can't create mqttd state dir {}", state_dir);
    fs::create_dir_all(state_dir.clone()).expect(err_message.as_str());

    let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
    let state = persistor.load().await?;
    info!("state loaded.");

    Ok((persistor, state))
}

async fn start_snapshotter(
    broker_handle: BrokerHandle,
    persistor: FilePersistor<VersionedFileFormat>,
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
        Duration::from_secs(5 * 60),
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
        if let Err(e) = broker_handle
            .send(Message::System(SystemEvent::StateSnapshot(
                snapshot_handle.clone(),
            )))
            .await
        {
            warn!(message = "failed to tick the snapshotter", error=%e);
        }
    }
}

#[derive(Debug, thiserror::Error)]
#[error("An error occurred loading configuration.")]
pub struct LoadConfigurationError;
