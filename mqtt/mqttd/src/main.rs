use failure::ResultExt;
use futures_util::pin_mut;
use native_tls::Identity;
use std::{env, io};
use tokio::fs;
use tokio::time::Duration;
use tracing::{info, warn, Level};
use tracing_subscriber::{fmt, EnvFilter};

use mqtt_broker::*;

use mqttd::{shutdown, snapshot};

#[tokio::main]
async fn main() -> Result<(), Error> {
    let subscriber = fmt::Subscriber::builder()
        .with_ansi(atty::is(atty::Stream::Stderr))
        .with_max_level(Level::TRACE)
        .with_writer(io::stderr)
        .with_env_filter(EnvFilter::from_default_env())
        .finish();
    let _ = tracing::subscriber::set_global_default(subscriber);

    let addr_tcp = env::args()
        .nth(1)
        .unwrap_or_else(|| "0.0.0.0:1883".to_string());

    let addr_tls = env::args()
        .nth(2)
        .unwrap_or_else(|| "0.0.0.0:8883".to_string());

    let cert_path = env::args()
        .nth(3)
        .unwrap_or_else(|| "broker.pfx".to_string());

    let identity = load_identity(cert_path)
        .await
        .context(ErrorKind::IdentityConfiguration)?;

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
    let broker = Broker::from_state(state);
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

    let transports = vec![(addr_tcp).into(), (addr_tls, identity).into()];

    info!("Starting server...");
    let state = Server::from_broker(broker)
        .serve(transports, shutdown)
        .await?;

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

async fn load_identity(path: String) -> Result<Identity, Error> {
    let cert_buffer = fs::read(&path).await.context(ErrorKind::LoadIdentity)?;

    let cert_pwd = "";
    let cert = Identity::from_pkcs12(cert_buffer.as_slice(), &cert_pwd)
        .context(ErrorKind::DecodeIdentity)?;

    Ok(cert)
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
