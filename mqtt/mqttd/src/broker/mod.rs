mod bootstrap;
mod shutdown;
mod snapshot;

use std::{fs, path::Path};

use anyhow::{Context, Result};
use tracing::{error, info};

use mqtt_broker::{BrokerReady, FilePersistor, Persist, VersionedFileFormat};

use crate::broker::snapshot::start_snapshotter;

use self::bootstrap::Bootstrap;

pub async fn run<P>(config_path: Option<P>) -> Result<()>
where
    P: AsRef<Path>,
{
    let settings = bootstrap::config(config_path).context(LoadConfigurationError)?;
    let listener_settings = settings.listener().clone();

    info!("loading state...");
    let persistence_config = settings.broker().persistence();
    let state_dir = persistence_config.file_path();

    fs::create_dir_all(state_dir.clone())?;
    let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
    let state = persistor.load().await?;
    info!("state loaded.");

    let broker_ready = BrokerReady::new();

    let broker = bootstrap::broker(settings.broker(), state, &broker_ready).await?;
    let broker_handle = broker.handle();

    let snapshot_interval = persistence_config.time_interval();
    let (mut snapshotter_shutdown_handle, snapshotter_join_handle) =
        start_snapshotter(broker.handle(), persistor, snapshot_interval).await;

    let shutdown_signal = shutdown::shutdown();
    let server = bootstrap::start_server(settings, broker, shutdown_signal, broker_ready);

    let mut bootstrap = Bootstrap::new();
    bootstrap::add_sidecars(&mut bootstrap, broker_handle.clone(), listener_settings)?;
    let state = bootstrap.run(broker_handle, server).await?;

    snapshotter_shutdown_handle.shutdown().await?;
    let mut persistor = snapshotter_join_handle.await?;
    info!("state snapshotter shutdown.");

    info!("persisting state before exiting...");
    persistor.store(state).await?;
    info!("state persisted.");

    info!("exiting... goodbye");
    Ok(())
}

#[derive(Debug, thiserror::Error)]
#[error("An error occurred loading configuration.")]
pub struct LoadConfigurationError;
