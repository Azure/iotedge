use std::{env, io};

use failure::ResultExt;
use futures_util::pin_mut;
use mqtt_broker::*;
use tokio::time::{Duration, Instant};
use tracing::{info, warn, Level};
use tracing_subscriber::{fmt, EnvFilter};

use mqttd::{shutdown, snapshot, Terminate};

#[tokio::main]
async fn main() -> Result<(), Terminate> {
    let subscriber = fmt::Subscriber::builder()
        .with_ansi(atty::is(atty::Stream::Stderr))
        .with_max_level(Level::TRACE)
        .with_writer(io::stderr)
        .with_env_filter(EnvFilter::from_default_env())
        .finish();
    let _ = tracing::subscriber::set_global_default(subscriber);

    let addr = env::args()
        .nth(1)
        .unwrap_or_else(|| "0.0.0.0:1883".to_string());

    // Setup the shutdown handle
    let shutdown = shutdown::shutdown();
    pin_mut!(shutdown);

    // Setup the snapshotter
    let mut persistor = FilePersistor::new(
        env::current_dir().expect("can't get cwd").join("state"),
        BincodeFormat::new(),
    );
    info!("Loading state...");
    let state = persistor.load().await?.unwrap_or_else(BrokerState::default);
    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(false))
        .state(state)
        .build();
    info!("state loaded.");

    let snapshotter = Snapshotter::new(persistor);
    let snapshot_handle = snapshotter.snapshot_handle();
    let mut shutdown_handle = snapshotter.shutdown_handle();
    let join_handle = tokio::spawn(snapshotter.run());

    // Tick the snapshotter
    let tick = tick_snapshot(
        Duration::from_secs(5 * 60),
        broker.handle(),
        snapshot_handle.clone(),
    );
    tokio::spawn(tick);

    // Signal the snapshotter
    let snapshot = snapshot::snapshot(broker.handle(), snapshot_handle.clone());
    tokio::spawn(snapshot);

    info!("Starting server...");
    let state = Server::from_broker(broker).serve(addr, shutdown).await?;

    // Stop snapshotting
    shutdown_handle.shutdown().await?;
    let mut persistor = join_handle.await.context(ErrorKind::TaskJoin)?;
    info!("state snapshotter shutdown.");

    info!("persisting state before exiting...");
    persistor.store(state).await?;
    info!("state persisted.");
    info!("exiting... goodbye");

    Ok(())
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
