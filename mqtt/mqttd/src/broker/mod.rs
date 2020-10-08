mod bootstrap;
mod shutdown;
mod snapshot;

use std::{fs, path::Path};

use anyhow::{Context, Result};
use futures_util::{
    future::{select, Either},
    pin_mut,
};
use tracing::{error, info};

use mqtt_broker::{FilePersistor, Message, Persist, SystemEvent, VersionedFileFormat};

use crate::broker::snapshot::start_snapshotter;

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

    let broker = bootstrap::broker(settings.broker(), state).await?;
    let mut broker_handle = broker.handle();

    let snapshot_interval = persistence_config.time_interval();
    let (mut snapshotter_shutdown_handle, snapshotter_join_handle) =
        start_snapshotter(broker.handle(), persistor, snapshot_interval).await;

    let shutdown_signal = shutdown::shutdown();

    // start broker
    let server_join_handle =
        tokio::spawn(bootstrap::start_server(settings, broker, shutdown_signal));

    // start sidecars if they should run
    // if not wait for server shutdown
    let state;
    if let Some(sidecar_manager) =
        bootstrap::start_sidecars(broker_handle.clone(), listener_settings).await?
    {
        // wait on future for sidecars or broker
        // if one of them exits then shut the other down
        let sidecar_shutdown_handle = sidecar_manager.shutdown_handle();
        let sidecars_fut = sidecar_manager.wait_for_shutdown();
        pin_mut!(sidecars_fut);
        state = match select(server_join_handle, sidecars_fut).await {
            // server finished first
            Either::Left((server_output, sidecars_fut)) => {
                // shutdown sidecars
                sidecar_shutdown_handle.shutdown().await?;

                // wait for sidecars to finish
                if let Err(e) = sidecars_fut.await {
                    error!(message = "failed running sidecars", err = %e)
                }

                // extract state from server
                server_output??
            }
            // sidecars finished first
            Either::Right((_, server_join_handle)) => {
                // signal server and sidecars shutdown
                broker_handle.send(Message::System(SystemEvent::Shutdown))?;

                // extract state from server
                let server_output = server_join_handle.await;
                server_output??
            }
        };
    } else {
        let server_output = server_join_handle.await;
        state = server_output??;
    }

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
