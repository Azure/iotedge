use std::{convert::TryInto, env, io};

use clap::{crate_description, crate_name, crate_version, App, Arg};
use futures_util::pin_mut;
use tokio::time::{Duration, Instant};
use tracing::{info, warn, Level};
use tracing_subscriber::{fmt, EnvFilter};

use mqtt_broker::*;
use mqtt_broker_core::auth::Authorizer;
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

    run().await?;
    Ok(())
}

async fn run() -> Result<(), Error> {
    let config = create_app()
        .get_matches()
        .value_of("config")
        .map_or(Ok(BrokerConfig::default()), BrokerConfig::from_file)
        .map_err(InitializeBrokerError::LoadConfiguration)?;

    // Setup the snapshotter
    let mut persistor = FilePersistor::new(
        env::current_dir().expect("can't get cwd").join("state"),
        VersionedFileFormat::default(),
    );
    info!("Loading state...");
    let state = persistor.load().await?.unwrap_or_else(BrokerSnapshot::default);
    let broker = BrokerBuilder::default()
        .with_authorizer(authorizer())
        .with_state(state)
        .with_config(config.clone())
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
    let state = run_broker_server(broker, config).await?;

    // Stop snapshotting
    shutdown_handle.shutdown().await?;
    let mut persistor = join_handle.await?;
    info!("state snapshotter shutdown.");

    info!("persisting state before exiting...");
    persistor.store(state).await?;
    info!("state persisted.");
    info!("exiting... goodbye");

    Ok(())
}

async fn run_broker_server<Z>(broker: Broker<Z>, config: BrokerConfig) -> Result<BrokerSnapshot, Error>
where
    Z: Authorizer + Send + Sync + 'static,
{
    // Setup the shutdown handle
    let shutdown = shutdown::shutdown();
    pin_mut!(shutdown);

    // Setup broker with previous state and with configured transports
    let mut server = Server::from_broker(broker);
    for config in config.transports() {
        let new_transport = config.clone().try_into()?;
        server.transport(new_transport, authenticator());
    }

    // When in edgehub mode add additional transport for internal communication
    #[cfg(feature = "edgehub")]
    {
        use mqtt_edgehub::auth::LocalAuthenticator;
        let new_transport = TransportBuilder::Tcp("localhost:1882".to_string());
        let authenticator = LocalAuthenticator::new();
        server.transport(new_transport, authenticator);
    }

    // Run server
    let state = server.serve(shutdown).await?;
    Ok(state)
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

fn create_app() -> App<'static, 'static> {
    App::new(crate_name!())
        .version(crate_version!())
        .about(crate_description!())
        .arg(
            Arg::with_name("config")
                .short("c")
                .long("config")
                .value_name("FILE")
                .help("Sets a custom config file")
                .takes_value(true),
        )
}
