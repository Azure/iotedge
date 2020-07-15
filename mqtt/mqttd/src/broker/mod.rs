mod bootstrap;
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

use futures_util::StreamExt;
use mqtt3;
use mqtt3::proto;

// TODO: rename to command handler and refactor
// TODO: move code to separate module / code file
async fn start_disconnect_watcher(broker_handle: BrokerHandle) {
    // TODO: get device id from env
    let client_id = "deviceid/$edgeHub/$broker/$control";
    let username = "";

    let mut client = mqtt3::Client::new(
        Some(client_id.to_string()),
        Some(username.to_string()),
        None,
        move || {
            let password = "";
            Box::pin(async move {
                let io = tokio::net::TcpStream::connect("127.0.0.1:1883").await; // TODO: read from config or broker
                io.map(|io| (io, Some(password.to_string())))
            })
        },
        Duration::from_secs(1),
        Duration::from_secs(60),
    );

    // TODO: handle result
    let topic_filter = "$edgehub/{}/disconnect".to_string();
    let qos = proto::QoS::AtLeastOnce;
    client.subscribe(proto::SubscribeTo { topic_filter, qos });

    while let Some(event) = client.next().await {
        info!("received data")
        // parse client id from topic into ClientId obj
        // send system message to broker handle
    }
}

pub async fn run(config: BrokerConfig) -> Result<()> {
    info!("loading state...");
    let state_dir = env::current_dir().expect("can't get cwd").join("state");
    let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
    let state = persistor.load().await?;
    info!("state loaded.");

    let broker = bootstrap::broker(&config, state).await?;

    info!("starting snapshotter...");
    let (mut shutdown_handle, join_handle) = start_snapshotter(broker.handle(), persistor).await;

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
    let server = bootstrap::server(config, broker).await?;
    let state = server.serve(shutdown).await?;

    Ok(state)
}
