mod bootstrap;
mod shutdown;
mod snapshot;

use std::{env, path::Path};

use anyhow::{Context, Result};
use futures_util::pin_mut;
use tokio::{
    task::JoinHandle,
    time::{Duration, Instant},
};
use tracing::{info, warn};

use mqtt_broker::{
    BrokerHandle, FilePersistor, Message, Persist, ShutdownHandle as SnapshotShutdownHandle,
    Snapshotter, StateSnapshotHandle, SystemEvent, VersionedFileFormat,
};

// TODO SIDECAR: remove all cfg
#[cfg(feature = "edgehub")]
use mqtt_edgehub::command::{CommandHandler, ShutdownHandle as CommandShutdownHandle};
const DEVICE_ID_ENV: &str = "IOTEDGE_DEVICEID";

pub async fn run<P>(config_path: Option<P>) -> Result<()>
where
    P: AsRef<Path>,
{
    /*
    initialize new bootstrap struct
    bootstrap.start_server
    bootstrap.start_sidecars
    wait on bootstrap handle
    */

    let config = bootstrap::config(config_path).context(LoadConfigurationError)?;

    info!("loading state...");
    let state_dir = env::current_dir().expect("can't get cwd").join("state");
    let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
    let state = persistor.load().await?;
    info!("state loaded.");

    let broker = bootstrap::broker(config.broker(), state).await?;

    #[cfg(feature = "edgehub")]
    let broker_handle = broker.handle();
    let system_address = config.listener().system().addr().to_string();

    info!("starting snapshotter...");
    let (mut snapshotter_shutdown_handle, snapshotter_join_handle) =
        start_snapshotter(broker.handle(), persistor).await;

    let shutdown = shutdown::shutdown();
    pin_mut!(shutdown);

    info!("starting server...");
    let start_server = bootstrap::start_server(config, broker, shutdown);

    #[cfg(feature = "edgehub")]
    info!("starting command handler...");
    let (mut command_handler_shutdown_handle, command_handler_join_handle) =
        start_command_handler(broker_handle, system_address).await?;

    let state = start_server.await?;

    snapshotter_shutdown_handle.shutdown().await?;
    let mut persistor = snapshotter_join_handle.await?;
    info!("state snapshotter shutdown.");

    #[cfg(feature = "edgehub")]
    command_handler_shutdown_handle.shutdown().await?;
    command_handler_join_handle.await?;
    info!("command handler shutdown.");

    info!("persisting state before exiting...");
    persistor.store(state).await?;
    info!("state persisted.");
    info!("exiting... goodbye");

    Ok(())
}

async fn start_command_handler(
    broker_handle: BrokerHandle,
    system_address: String,
) -> Result<(CommandShutdownHandle, JoinHandle<()>)> {
    let device_id = env::var(DEVICE_ID_ENV)?;
    let command_handler = CommandHandler::new(broker_handle, system_address, device_id.as_str())?;
    let shutdown_handle = command_handler.shutdown_handle()?;

    let join_handle = tokio::spawn(command_handler.run());

    Ok((shutdown_handle, join_handle))
}

async fn start_snapshotter(
    broker_handle: BrokerHandle,
    persistor: FilePersistor<VersionedFileFormat>,
) -> (
    SnapshotShutdownHandle,
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
