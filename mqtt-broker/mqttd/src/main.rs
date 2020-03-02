use std::{env, io};

use failure::ResultExt;
use futures_util::pin_mut;
use mqtt_broker::*;
use tokio::signal::unix::SignalKind;
use tokio::time::Duration;
use tracing::{info, warn, Level};
use tracing_subscriber::{fmt, EnvFilter};

mod shutdown;

#[tokio::main]
async fn main() -> Result<(), Error> {
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
    let mut persistor = NullPersistor;
    info!("Loading state...");
    let state = persistor.load().await.context(ErrorKind::General)?;
    let broker = Broker::from_state(state);
    info!("state loaded.");

    let snapshotter = Snapshotter::new(NullPersistor);
    let snapshot_handle = snapshotter.snapshot_handle();
    let mut shutdown_handle = snapshotter.shutdown_handle();
    let join_handle = tokio::spawn(snapshotter.run());

    // Tick the snapshotter
    let tick = tick_snapshot(
        Duration::from_secs(5),
        broker.handle(),
        snapshot_handle.clone(),
    );
    tokio::spawn(tick);

    // Signal the snapshotter
    let signal = signal_snapshot(broker.handle(), snapshot_handle.clone());
    tokio::spawn(signal);

    info!("Starting server...");
    let state = Server::from_broker(broker).serve(addr, shutdown).await?;

    // Stop snapshotting
    shutdown_handle.shutdown().await?;
    let mut persistor = join_handle.await.context(ErrorKind::BrokerJoin)?;
    info!("state snapshotter shutdown.");

    info!("persisting state before exiting...");
    persistor.store(state).await?;
    info!("state persisted.");
    info!("exiting... good bye");

    Ok(())
}

async fn tick_snapshot(
    period: Duration,
    mut broker_handle: BrokerHandle,
    snapshot_handle: StateSnapshotHandle,
) {
    info!("Persisting state every {:?}", period);
    let mut interval = tokio::time::interval(period);
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

async fn signal_snapshot(mut broker_handle: BrokerHandle, snapshot_handle: StateSnapshotHandle) {
    let mut stream = match tokio::signal::unix::signal(SignalKind::user_defined1()) {
        Ok(stream) => stream,
        Err(e) => {
            warn!(message = "an error occurred setting up the signal handler", error=%e);
            return;
        }
    };

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
}
