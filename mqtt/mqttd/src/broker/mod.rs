mod bootstrap;
mod shutdown;
mod snapshot;

use std::{fs, path::Path};

use anyhow::{Context, Result};
use futures_util::future::{select, select_all, Either};
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

    // start sidecars
    // TODO REVIEW: it is fine to blow up here
    //              a failure here would indicate either bridge or command handler init failed
    //              broker doesn't open ports until these are both initialized
    let state;
    if let Some((sidecars_shutdown, sidecar_join_handles)) =
        bootstrap::start_sidecars(broker_handle.clone(), listener_settings).await?
    {
        // combine future for all sidecars
        // wait on future for sidecars or broker
        // if one of them exits then shut the other down
        // TODO REVIEW: we are only blowing up if either:
        //              1 - we can't stop the server and can't get the needed state
        //              2 - we can't signal server or sidecars to shutdown
        let sidecars_fut = select_all(sidecar_join_handles);
        state = match select(server_join_handle, sidecars_fut).await {
            // server finished first
            Either::Left((server_output, sidecar_join_handles)) => {
                // extract state from finished server
                let state = server_output??;

                // shutdown sidecars
                sidecars_shutdown.shutdown().await?;

                // collect join handles
                let (sidecar_output, _, other_handles) = sidecar_join_handles.await;

                // wait for sidecars to finish
                if let Err(e) = sidecar_output {
                    error!(message = "failed waiting for sidecar shutdown", err = %e);
                }
                for handle in other_handles {
                    if let Err(e) = handle.await {
                        error!(message = "failed waiting for sidecar shutdown", err = %e);
                    }
                }

                state
            }
            // a sidecar finished first
            Either::Right((sidecars_output, server_join_handle)) => {
                // collect join handles from sidecars
                let (completed_join_handle, _, other_handles) = sidecars_output;

                // wait for sidecars to finish
                if let Err(e) = completed_join_handle {
                    error!(message = "failed waiting for sidecar shutdown", err = %e);
                }
                for handle in other_handles {
                    if let Err(e) = handle.await {
                        error!(message = "failed waiting for sidecar shutdown", err = %e);
                    }
                }

                // signal server and sidecars shutdown
                sidecars_shutdown.shutdown().await?;
                broker_handle.send(Message::System(SystemEvent::Shutdown))?;

                // extract state from server
                server_join_handle.await??
            }
        };
    } else {
        state = server_join_handle.await??;
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
