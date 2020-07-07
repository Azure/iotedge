mod child_process;

use std::{
    io::Error,
    process::exit,
    sync::atomic::{AtomicBool, Ordering},
    sync::Arc,
};

use child_process::run_child_process;
use signal_hook::{flag::register, SIGINT, SIGTERM};
use tracing::{error, info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

fn main() -> Result<(), Error> {
    init_logging();
    info!("Starting watchdog");

    let should_shutdown = match register_shutdown_listener() {
        Ok(should_shutdown) => should_shutdown,
        Err(e) => {
            error!(
                "Failed to register sigterm listener. Shutting down. {:?}",
                e
            );
            exit(1);
        }
    };

    let broker_handle = run_child_process(
        "MQTT Broker".to_string(),
        "/usr/local/bin/mqttd".to_string(),
        "-c /tmp/mqtt/config/production.json".to_string(),
        Arc::clone(&should_shutdown),
    );

    let edgehub_handle = run_child_process(
        "Edge Hub".to_string(),
        "dotnet".to_string(),
        "/app/Microsoft.Azure.Devices.Edge.Hub.Service.dll".to_string(),
        Arc::clone(&should_shutdown),
    );

    match broker_handle.join() {
        Ok(()) => info!("Successfully stopped broker process"),
        Err(e) => {
            should_shutdown.store(true, Ordering::Relaxed);
            error!("Failure while running broker process. {:?}", e)
        }
    };
    match edgehub_handle.join() {
        Ok(()) => info!("Successfully stopped edgehub process"),
        Err(e) => {
            should_shutdown.store(true, Ordering::Relaxed);
            error!("Failure while running edgehub process. {:?}", e)
        }
    };

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
