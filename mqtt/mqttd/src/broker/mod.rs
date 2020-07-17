mod bootstrap;
mod command;
mod shutdown;
mod snapshot;

use std::env;

use anyhow::Result;
use futures_util::pin_mut;
use tokio::{
    task::JoinHandle,
    time::{Duration, Instant},
};
use tracing::{info, warn};

use command::CommandHandler;
use command::ShutdownHandle as CommandShutdownHandle;
use mqtt_broker::{
    Broker, BrokerConfig, BrokerHandle, BrokerSnapshot, FilePersistor, Message, Persist,
    ShutdownHandle as SnapshotShutdownHandle, Snapshotter, StateSnapshotHandle, SystemEvent,
    VersionedFileFormat,
};
use mqtt_broker_core::auth::Authorizer;

pub async fn run(config: BrokerConfig) -> Result<()> {
    info!("loading state...");
    let state_dir = env::current_dir().expect("can't get cwd").join("state");
    let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
    let state = persistor.load().await?;
    info!("state loaded.");

    let broker = bootstrap::broker(&config, state).await?;

    #[cfg(feature = "edgehub")]
    info!("starting command handler...");
    let (mut command_handler_shutdown_handle, command_handler_join_handle) =
        start_command_handler(broker.handle()).await;

    info!("starting snapshotter...");
    let (mut snapshotter_shutdown_handle, snapshotter_join_handle) =
        start_snapshotter(broker.handle(), persistor).await;

    info!("starting server...");
    let state = start_server(&config, broker).await?;

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
) -> (CommandShutdownHandle, JoinHandle<()>) {
    let command_handler = CommandHandler::new(broker_handle);
    let shutdown_handle = command_handler.shutdown_handle();

    let join_handle = tokio::spawn(command_handler.run());

    (shutdown_handle, join_handle)
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

async fn start_server<Z>(config: &BrokerConfig, broker: Broker<Z>) -> Result<BrokerSnapshot>
where
    Z: Authorizer + Send + 'static,
{
    // Setup the shutdown handle
    let shutdown = shutdown::shutdown();
    pin_mut!(shutdown);

    // Run server
    let server = bootstrap::server(config, broker).await?;
    let state = server.serve(shutdown).await?;

    Ok(state)
}
