mod bootstrap;
mod shutdown;
mod snapshot;

use std::{fs, path::PathBuf};

use anyhow::Result;
use futures_util::pin_mut;
use tokio::{
    task::JoinHandle,
    time::{Duration, Instant},
};
use tracing::{info, warn};

use mqtt_broker::{
    Broker, BrokerConfig, BrokerHandle, BrokerSnapshot, FilePersistor, Message, Persist,
    ShutdownHandle, Snapshotter, StateSnapshotHandle, SystemEvent, VersionedFileFormat,
};
use mqtt_broker_core::auth::Authorizer;

const MQTTD_STATE_DIR: &str = "/tmp/mqttd";

pub async fn run(config: BrokerConfig) -> Result<()> {
    info!("loading state...");
    fs::create_dir(MQTTD_STATE_DIR).expect("can't create mqttd state dir");
    let mut persistor = FilePersistor::new(
        PathBuf::from(MQTTD_STATE_DIR),
        VersionedFileFormat::default(),
    );
    let state = persistor.load().await?;
    info!("state loaded.");

    let broker = bootstrap::broker(&config, state).await?;

    info!("starting snapshotter...");
    let (mut shutdown_handle, join_handle) = start_snapshotter(broker.handle(), persistor).await;

    info!("starting server...");
    let state = start_server(&config, broker).await?;

    shutdown_handle.shutdown().await?;
    let mut persistor = join_handle.await?;
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

async fn start_server<Z>(config: &BrokerConfig, broker: Broker<Z>) -> Result<BrokerSnapshot>
where
    Z: Authorizer + Send + 'static,
{
    // Setup the shutdown handle
    let shutdown = shutdown::shutdown();
    pin_mut!(shutdown);

    // Run server
    bootstrap::start_server(config, broker, shutdown).await
}
