mod bootstrap;
mod command_handler;
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

use mqtt_broker::{
    Broker, BrokerConfig, BrokerHandle, BrokerSnapshot, FilePersistor, Message, Persist,
    ShutdownHandle, Snapshotter, StateSnapshotHandle, SystemEvent, VersionedFileFormat,
};
use mqtt_broker_core::auth::Authorizer;

pub async fn run(config: BrokerConfig) -> Result<()> {
    info!("loading state...");
    let state_dir = env::current_dir().expect("can't get cwd").join("state");
    let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
    let state = persistor.load().await?;
    info!("state loaded.");

    let broker = bootstrap::broker(&config, state).await?;

    // TODO: do this only if in edgehub mode (cfg?)
    info!("starting command handler...");
    let command_handler_join_handle = start_command_handler(broker.handle()).await;

    info!("starting snapshotter...");
    let (mut snapshotter_shutdown_handle, snapshotter_join_handle) =
        start_snapshotter(broker.handle(), persistor).await;

    /* TODO: Call func start_disconnect_watcher() that will:
        1. Create a custom client with clean session
            client_id: Option<String>, = disconnect-watcher-[RANDOM_HASH]
            username: Option<String>, = disconnect-watcher-[RANDOM_HASH]
            will: Option<crate::proto::Publication>, = None
            io_source: IoS, = reference the code block below
            max_reconnect_back_off: std::time::Duration, = 1 sec in our tests
            keep_alive: std::time::Duration, = 60 sec in our tests. what is this though?
        2. The client knows what messages are received through the poll_next() call? But this isn't public?
           When topics subscription yields messages it will call system signal for client disconnection (similar to the snapshot below).
    */

    /*
    let io_source = move || {
            let address = address.clone();
            let password = password.clone();
            Box::pin(async move {
                let io = tokio::net::TcpStream::connect(address).await;
                io.map(|io| (io, password))
            })
        };
    */

    /*
    info!("Setup to persist state on USR1 signal");
        loop {
            stream.recv().await;
            info!("Received signal USR1");
            if let Err(e) = broker_handle
                .send(Message::System(SystemEvent::StateSnapshot(
                    snapshot_handle.clone(),
                )))
                .await
            {
                warn!(message = "failed to signal the snapshotter", error=%e);
            }
        }
    */

    info!("starting server...");
    let state = start_server(&config, broker).await?;

    snapshotter_shutdown_handle.shutdown().await?;
    let mut persistor = snapshotter_join_handle.await?;
    info!("state snapshotter shutdown.");

    command_handler_join_handle.await?;
    info!("command handler shutdown.");

    info!("persisting state before exiting...");
    persistor.store(state).await?;
    info!("state persisted.");
    info!("exiting... goodbye");

    Ok(())
}

async fn start_command_handler(broker_handle: BrokerHandle) -> JoinHandle<()> {
    let command_handler = command_handler::CommandHandler::new(broker_handle);

    //TODO: confirm we don't need this because on shutdown we shouldn't be forcibly disconnecting clients
    //      this command handler is just listening to edgehub topics
    // let shutdown_handle = command_handler.shutdown_handle();

    let join_handle = tokio::spawn(command_handler.run());
    join_handle
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
    let server = bootstrap::server(config, broker).await?;
    let state = server.serve(shutdown).await?;

    Ok(state)
}
