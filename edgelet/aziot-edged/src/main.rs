// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms)]
#![warn(clippy::all, clippy::pedantic)]

mod error;
mod management;
mod provision;

// TODO: Remove this with parent_hostname_resolve
use edgelet_core::RuntimeSettings;

use crate::error::Error as EdgedError;

#[tokio::main]
async fn main() {
    let version = edgelet_core::version_with_source_version();

    clap::App::new(clap::crate_name!())
        .version(version.as_str())
        .author(clap::crate_authors!("\n"))
        .about(clap::crate_description!())
        .get_matches();

    logger::try_init()
        .expect("cannot fail to initialize global logger from the process entrypoint");

    log::info!("Starting Azure IoT Edge Daemon");
    log::info!("Version - {}", edgelet_core::version_with_source_version());

    if let Err(err) = run().await {
        log::error!("{}", err);

        std::process::exit(err.into());
    }
}

async fn run() -> Result<(), EdgedError> {
    let settings = edgelet_docker::Settings::new()?;

    let cache_dir = std::path::Path::new(&settings.base.homedir).join("cache");
    std::fs::create_dir_all(cache_dir.clone()).map_err(|err| {
        EdgedError::new(format!(
            "Failed to create cache directory {}: {}",
            cache_dir.as_path().display(),
            err
        ))
    })?;

    let device_info = provision::get_device_info(&settings, &cache_dir).await?;

    // Normally, aziot-edged will stop all modules when it shuts down. But if it crashed,
    // modules will continue to run. On Linux systems where aziot-edged is responsible for
    // creating/binding the socket (e.g., CentOS 7.5, which uses systemd but does not
    // support systemd socket activation), modules will be left holding stale file
    // descriptors for the workload and management APIs and calls on these APIs will
    // begin to fail. Resilient modules should be able to deal with this, but we'll
    // restart all modules to ensure a clean start.
    log::info!("Stopping all modules...");
    // TODO
    log::info!("All modules stopped");

    provision::update_device_cache(&cache_dir, &device_info)?;

    // TODO: Rework settings so this isn't needed.
    let mut settings = settings;
    settings
        .agent_mut()
        .parent_hostname_resolve(&device_info.gateway_host);

    let (sender, mut receiver) =
        tokio::sync::mpsc::unbounded_channel::<edgelet_core::ShutdownReason>();

    // Set the signal handler to listen for CTRL+C (SIGINT).
    let sigint_sender = sender.clone();
    tokio::spawn(async move {
        tokio::signal::ctrl_c()
            .await
            .expect("cannot fail to set signal handler");

        // Failure to send the shutdown signal means that the mpsc queue is closed.
        // Ignore this Result, as the process will be shutting down anyways.
        let _ = sigint_sender.send(edgelet_core::ShutdownReason::SigInt);
    });

    // Start management and workload sockets.
    management::start(&settings, sender).await?;

    if let Some(shutdown) = receiver.recv().await {
        log::info!("{}", shutdown);
    }

    Ok(())
}
