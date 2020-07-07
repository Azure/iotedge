mod child;

use std::{
    io::Error,
    sync::atomic::{AtomicBool, Ordering},
    sync::Arc,
};

use anyhow::{Context, Result};
use child::run;
use signal_hook::{flag::register, SIGINT, SIGTERM};
use tracing::{error, info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

fn main() -> Result<()> {
    init_logging();
    info!("Starting watchdog");

    let should_shutdown = register_shutdown_listener()
        .context("Failed to register sigterm listener. Shutting down.")?;

    let broker_handle = run(
        "MQTT Broker".to_string(),
        "/usr/local/bin/mqttd".to_string(),
        "-c /tmp/mqtt/config/production.json".to_string(),
        Arc::clone(&should_shutdown),
    )?;

    let edgehub_handle = match run(
        "Edge Hub".to_string(),
        "dotnet".to_string(),
        "/app/Microsoft.Azure.Devices.Edge.Hub.Service.dll".to_string(),
        Arc::clone(&should_shutdown),
    ) {
        Ok(handle) => Some(handle),
        Err(e) => {
            should_shutdown.store(true, Ordering::Relaxed);
            error!("Could not start edgehub process. {}", e);
            None
        }
    };

    if let Err(e) = broker_handle.join() {
        should_shutdown.store(true, Ordering::Relaxed);
        error!("Failure while running broker process. {:?}", e)
    }
    info!("Successfully stopped broker process");

    edgehub_handle.map(|handle| {
        if let Err(e) = handle.join() {
            should_shutdown.store(true, Ordering::Relaxed);
            error!("Failure while running edgehub process. {:?}", e);
        }
        info!("Successfully stopped edgehub process");
    });

    info!("Stopped watchdog process");
    Ok(())
}

fn init_logging() {
    let subscriber = Subscriber::builder().with_max_level(Level::INFO).finish();
    let _ = subscriber::set_global_default(subscriber);
}

fn register_shutdown_listener() -> Result<Arc<AtomicBool>, Error> {
    info!("Registering shutdown signal listener");
    let should_shutdown = Arc::new(AtomicBool::new(false));
    register(SIGTERM, Arc::clone(&should_shutdown))?;
    register(SIGINT, Arc::clone(&should_shutdown))?;
    Ok(should_shutdown)
}
