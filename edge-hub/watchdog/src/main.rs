#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
    clippy::match_same_arms,
    clippy::must_use_candidate,
    clippy::missing_errors_doc
)]

use std::{
    io::Error,
    sync::atomic::{AtomicBool, Ordering},
    sync::Arc,
    thread,
};

use anyhow::{Context, Result};
use child::run;
use signal_hook::{iterator::Signals, SIGINT, SIGTERM};
use tracing::{error, info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

mod child;

fn main() -> Result<()> {
    init_logging();
    info!("Starting Watchdog");

    let should_shutdown = register_shutdown_listener()
        .context("Failed to register sigterm listener. Shutting down.")?;

    let edgehub_handle = run(
        "Edge Hub".to_string(),
        "dotnet".to_string(),
        "/app/Microsoft.Azure.Devices.Edge.Hub.Service.dll".to_string(),
        Arc::clone(&should_shutdown),
    )?;

    let broker_handle = match run(
        "MQTT Broker".to_string(),
        "/usr/local/bin/mqttd".to_string(),
        "-c /tmp/mqtt/config/production.json".to_string(),
        Arc::clone(&should_shutdown),
    ) {
        Ok(handle) => Some(handle),
        Err(e) => {
            should_shutdown.store(true, Ordering::Relaxed);
            error!("Could not start MQTT Broker process. {}", e);
            None
        }
    };

    if let Err(e) = edgehub_handle.join() {
        should_shutdown.store(true, Ordering::Relaxed);
        error!("Failure while running Edge Hub process. {:?}", e)
    } else {
        info!("Successfully stopped Edge Hub process");
    }

    if let Some(handle) = broker_handle {
        if let Err(e) = handle.join() {
            should_shutdown.store(true, Ordering::Relaxed);
            error!("Failure while running MQTT Broker process. {:?}", e);
        } else {
            info!("Successfully stopped MQTT Broker process");
        }
    }

    info!("Stopped Watchdog process");
    Ok(())
}

fn init_logging() {
    let subscriber = Subscriber::builder().with_max_level(Level::INFO).finish();
    let _ = subscriber::set_global_default(subscriber);
}

fn register_shutdown_listener() -> Result<Arc<AtomicBool>, Error> {
    info!("Registering shutdown signal listener");
    let shutdown_listener = Arc::new(AtomicBool::new(false));
    let should_shutdown = shutdown_listener.clone();
    let signals = Signals::new(&[SIGTERM, SIGINT])?;
    thread::spawn(move || {
        for signal in signals.forever().filter_map(|signal| match signal {
            SIGTERM => Some("SIGTERM"),
            SIGINT => Some("SIGINT"),
            _ => None,
        }) {
            info!("Watchdog received {} signal", signal);
            shutdown_listener.store(true, Ordering::Relaxed);
        }
    });

    Ok(should_shutdown)
}
