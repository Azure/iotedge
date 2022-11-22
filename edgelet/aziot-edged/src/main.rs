// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms)]
#![warn(clippy::all, clippy::pedantic)]

mod error;
mod management;
mod provision;
mod watchdog;
mod workload_manager;

use std::sync::atomic;

use edgelet_core::{module::ModuleAction, ModuleRuntime, WatchdogAction};
use edgelet_docker::{ImagePruneData, MakeModuleRuntime};
use edgelet_image_cleanup::image_gc;
use edgelet_settings::RuntimeSettings;

use crate::{error::Error as EdgedError, workload_manager::WorkloadManager};

#[tokio::main]
async fn main() {
    let version = edgelet_core::version_with_source_version();

    clap::Command::new(clap::crate_name!())
        .version(&version)
        .author(clap::crate_authors!("\n"))
        .about(clap::crate_description!())
        .get_matches();

    logger::try_init()
        .expect("cannot fail to initialize global logger from the process entrypoint");

    log::info!("Starting Azure IoT Edge Daemon");
    log::info!("Version - {}", version);

    if let Err(err) = run().await {
        if err.exit_code() == EdgedError::reprovisioned().exit_code() {
            log::info!("{}", err);
        } else {
            log::error!("{}", err);
        }

        std::process::exit(err.into());
    }
}

#[allow(clippy::too_many_lines)]
async fn run() -> Result<(), EdgedError> {
    let settings =
        edgelet_settings::docker::Settings::new().map_err(|err| EdgedError::settings_err(err))?;

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

    let mnt_dir = std::path::Path::new(&settings.homedir()).join("mnt");
    std::fs::create_dir_all(&mnt_dir).map_err(|err| {
        EdgedError::from_err(
            format!(
                "Failed to create mnt directory {}",
                mnt_dir.as_path().display()
            ),
            err,
        )
    })?;

    let gc_dir = std::path::Path::new(&settings.homedir()).join("gc");
    std::fs::create_dir_all(&gc_dir).map_err(|err| {
        EdgedError::from_err(
            format!(
                "Failed to create gc directory {}",
                gc_dir.as_path().display()
            ),
            err,
        )
    })?;

    let identity_client = provision::identity_client(&settings)?;

    let device_info = provision::get_device_info(
        &identity_client,
        settings.auto_reprovisioning_mode(),
        &cache_dir,
    )
    .await?;

    let (create_socket_channel_snd, create_socket_channel_rcv) =
        tokio::sync::mpsc::unbounded_channel::<ModuleAction>();

    let gc_settings = settings.image_garbage_collection().clone();

    let image_use_data = ImagePruneData::new(&gc_dir, gc_settings.clone())
        .map_err(|err| EdgedError::from_err("Failed to set up image garbage collection", err))?;

    let runtime = edgelet_docker::DockerModuleRuntime::make_runtime(
        &settings,
        create_socket_channel_snd.clone(),
        image_use_data.clone(),
    )
    .await
    .map_err(|err| EdgedError::from_err("Failed to initialize module runtime", err))?;

    let (watchdog_tx, watchdog_rx) =
        tokio::sync::mpsc::unbounded_channel::<edgelet_core::WatchdogAction>();

    // Keep track of running tasks to determine when all server tasks have shut down.
    // Workload and management API each have one task, so start with 2 tasks total.
    let tasks = atomic::AtomicUsize::new(2);
    let tasks = std::sync::Arc::new(tasks);

    // Workload manager needs to start before modules can be stopped.
    let (workload_manager, workload_shutdown) = WorkloadManager::start(
        &settings,
        runtime.clone(),
        &device_info,
        tasks.clone(),
        create_socket_channel_snd,
        watchdog_tx.clone(),
        settings.iotedge_max_requests().workload,
    )
    .await?;

    // Normally, aziot-edged will stop all modules when it shuts down. But if it crashed,
    // modules will continue to run. On Linux systems where aziot-edged is responsible for
    // creating/binding the socket (e.g., CentOS 7.5, which uses systemd but does not
    // support systemd socket activation), modules will be left holding stale file
    // descriptors for the workload and management APIs and calls on these APIs will
    // begin to fail. Resilient modules should be able to deal with this, but we'll
    // restart all modules to ensure a clean start.
    log::info!("Stopping all modules...");
    if let Err(err) = runtime
        .stop_all(Some(std::time::Duration::from_secs(30)))
        .await
    {
        log::warn!("Failed to stop modules on startup: {}", err);
    } else {
        log::info!("All modules stopped");
    }

    provision::update_device_cache(&cache_dir, &device_info, &runtime).await?;

    // Resolve the parent hostname used to pull Edge Agent. This translates '$upstream' into the
    // appropriate hostname.
    let settings = settings.agent_upstream_resolve(&device_info.gateway_host);

    // Start management and workload sockets.
    let management_shutdown = management::start(
        &settings,
        runtime.clone(),
        watchdog_tx.clone(),
        tasks.clone(),
        settings.iotedge_max_requests().management,
    )
    .await?;

    workload_manager::server(workload_manager, runtime.clone(), create_socket_channel_rcv).await?;

    // Set signal handlers for SIGTERM and SIGINT.
    set_signal_handlers(watchdog_tx);

    let shutdown_reason: WatchdogAction;

    let watchdog = watchdog::run_until_shutdown(
        settings.clone(),
        &device_info,
        runtime.clone(),
        &identity_client,
        watchdog_rx,
    );

    let edge_agent_bootstrap: String = settings.agent().config().image().to_string();
    let image_gc = image_gc::image_garbage_collect(
        edge_agent_bootstrap,
        gc_settings.clone(),
        &runtime,
        image_use_data,
    );

    tokio::select! {
        watchdog_finished = watchdog => {
            log::info!("watchdog finished");
            shutdown_reason = watchdog_finished?;
        },
        image_gc_finished = image_gc => {
            let err_msg = "image garbage collection stopped unexpectedly";
            image_gc_finished.map_err(|e| EdgedError::from_err(err_msg, e))?;
            return Err(EdgedError::new(err_msg));
        }
    };

    log::info!("Stopping management API...");
    management_shutdown
        .send(())
        .expect("management API shutdown receiver was dropped");

    log::info!("Stopping workload API...");
    workload_shutdown
        .send(())
        .expect("workload API shutdown receiver was dropped");

    let shutdown_timeout = std::time::Duration::from_secs(10);
    let poll_period = std::time::Duration::from_millis(100);
    let mut wait_time = std::time::Duration::from_millis(0);

    loop {
        let tasks = tasks.load(atomic::Ordering::Acquire);

        if tasks == 0 {
            break;
        }

        if wait_time >= shutdown_timeout {
            log::warn!("{} task(s) have not exited in time for shutdown", tasks);

            break;
        }

        tokio::time::sleep(poll_period).await;
        wait_time += poll_period;
    }

    if let edgelet_core::WatchdogAction::Reprovision = shutdown_reason {
        provision::reprovision(&identity_client, &cache_dir)
            .await
            .map_err(|err| EdgedError::from_err("Failed to reprovision", err))?;

        log::info!("Successfully reprovisioned");

        Err(EdgedError::reprovisioned())
    } else {
        Ok(())
    }
}

fn set_signal_handlers(
    shutdown_tx: tokio::sync::mpsc::UnboundedSender<edgelet_core::WatchdogAction>,
) {
    // Set the signal handler to listen for CTRL+C (SIGINT).
    let sigint_sender = shutdown_tx.clone();

    tokio::spawn(async move {
        tokio::signal::ctrl_c()
            .await
            .expect("cannot fail to set signal handler");

        // Failure to send the shutdown signal means that the mpsc queue is closed.
        // Ignore this Result, as the process will be shutting down anyways.
        let _ = sigint_sender.send(edgelet_core::WatchdogAction::Signal);
    });

    // Set the signal handler to listen for systemctl stop (SIGTERM).
    let mut sigterm_stream =
        tokio::signal::unix::signal(tokio::signal::unix::SignalKind::terminate())
            .expect("cannot fail to set signal handler");
    let sigterm_sender = shutdown_tx;

    tokio::spawn(async move {
        sigterm_stream.recv().await;

        // Failure to send the shutdown signal means that the mpsc queue is closed.
        // Ignore this Result, as the process will be shutting down anyways.
        let _ = sigterm_sender.send(edgelet_core::WatchdogAction::Signal);
    });
}
