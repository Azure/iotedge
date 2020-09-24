mod bootstrap;
mod shutdown;
mod snapshot;

use std::{fs, path::Path};

use anyhow::{Context, Result};
use futures_util::future::{select, select_all, Either};
use tracing::info;

use mqtt_broker::{FilePersistor, Message, Persist, SystemEvent, VersionedFileFormat};

use crate::broker::snapshot::start_snapshotter;

pub async fn run<P>(config_path: Option<P>) -> Result<()>
where
    P: AsRef<Path>,
{
    let settings = bootstrap::config(config_path).context(LoadConfigurationError)?;
    let system_address = settings.listener().system().addr().to_string();

    info!("loading state...");
    let persistence_config = settings.broker().persistence();
    let state_dir = persistence_config.file_path();

    fs::create_dir_all(state_dir.clone())?;
    let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
    let state = persistor.load().await?;
    info!("state loaded.");

    let broker = bootstrap::broker(settings.broker(), state).await?;
    let mut broker_handle = broker.handle();

    info!("starting snapshotter...");
    let snapshot_interval = persistence_config.time_interval();
    let (mut snapshotter_shutdown_handle, snapshotter_join_handle) =
        start_snapshotter(broker.handle(), persistor, snapshot_interval).await;

    let shutdown_signal = shutdown::shutdown();

    // start broker
    info!("starting server...");
    let server_join_handle =
        tokio::spawn(bootstrap::start_server(settings, broker, shutdown_signal));

    // start sidecars
    info!("starting sidecars...");
    let (sidecars_shutdown, sidecar_join_handles) =
        bootstrap::start_sidecars(broker_handle.clone(), system_address).await?;

    // combine future for all sidecars
    // wait on future for sidecars or broker
    // if one of them exits then shut the other down
    let sidecars_fut = select_all(sidecar_join_handles);
    let state = match select(server_join_handle, sidecars_fut).await {
        Either::Left((server_join_handle, sidecars_join_handle)) => {
            let state = server_join_handle??;

            sidecars_shutdown.shutdown().await?;
            let (completed_join_handle, _, other_handles) = sidecars_join_handle.await;
            completed_join_handle?;
            for handle in other_handles {
                handle.await?;
            }

            state
        }
        Either::Right((sidecars_join_handle, server_join_handle)) => {
            let (completed_join_handle, _, other_handles) = sidecars_join_handle;
            completed_join_handle?;
            for handle in other_handles {
                handle.await?;
            }

            sidecars_shutdown.shutdown().await?;
            broker_handle.send(Message::System(SystemEvent::Shutdown))?;
            let state = server_join_handle.await??;

            state
        }
    };

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
