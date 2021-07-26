// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms)]
#![warn(clippy::all, clippy::pedantic)]

mod error;
mod management;
mod provision;
mod watchdog;
mod workload;

use std::sync::atomic;

use edgelet_settings::RuntimeSettings;

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
    let settings =
        edgelet_settings::docker::Settings::new().map_err(|err| EdgedError::settings_err(&err))?;

    let cache_dir = std::path::Path::new(&settings.homedir()).join("cache");
    std::fs::create_dir_all(cache_dir.clone()).map_err(|err| {
        EdgedError::from_err(
            format!(
                "Failed to create cache directory {}",
                cache_dir.as_path().display()
            ),
            err,
        )
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

    // Resolve the parent hostname used to pull Edge Agent. This translates '$upstream' into the
    // appropriate hostname.
    let settings = settings.agent_upstream_resolve(&device_info.gateway_host);

    let (shutdown_tx, shutdown_rx) =
        tokio::sync::mpsc::unbounded_channel::<edgelet_core::ShutdownReason>();

    // Keep track of running tasks to determine when all server tasks have shut down.
    // Workload and management API each have one task, so start with 2 tasks total.
    let tasks = atomic::AtomicUsize::new(1); // TODO: change to 2 when management API is fixed
    let tasks = std::sync::Arc::new(tasks);

    // Start management and workload sockets.
    let management_shutdown = management::start(&settings, shutdown_tx.clone()).await?;
    let workload_shutdown = workload::start(&settings, &device_info, tasks.clone()).await?;

    // Set the signal handler to listen for CTRL+C (SIGINT).
    let sigint_sender = shutdown_tx.clone();
    tokio::spawn(async move {
        tokio::signal::ctrl_c()
            .await
            .expect("cannot fail to set signal handler");

        // Failure to send the shutdown signal means that the mpsc queue is closed.
        // Ignore this Result, as the process will be shutting down anyways.
        let _ = sigint_sender.send(edgelet_core::ShutdownReason::SigInt);
    });

    // Run aziot-edged until the shutdown signal is received. This also runs the watchdog periodically.
    watchdog::run_until_shutdown(&settings, shutdown_rx).await?;

    log::info!("Stopping management API...");
    // management_shutdown
    //     .send(())
    //     .expect("management API shutdown receiver was dropped");

    log::info!("Stopping workload API...");
    workload_shutdown
        .send(())
        .expect("workload API shutdown receiver was dropped");

    // Wait up to 10 seconds for all server tasks to exit.
    let poll_period = std::time::Duration::from_millis(100);
    let mut wait_time = std::time::Duration::from_millis(0);

    loop {
        let tasks = tasks.load(atomic::Ordering::Acquire);

        if tasks == 0 {
            break;
        }

        if wait_time >= std::time::Duration::from_secs(10) {
            log::warn!("{} task(s) have not exited in time for shutdown", tasks);

            break;
        }

        tokio::time::sleep(poll_period).await;
        wait_time += poll_period;
    }

    Ok(())
}
