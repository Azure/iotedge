use std::{
    io::Error,
    process::{exit, Child, Command, Stdio},
    sync::atomic::{AtomicBool, Ordering},
    sync::Arc,
    thread,
    time::Duration,,
};

use futures::executor::block_on;
use futures::join;
use nix::;, };, };
use nix::
    sys::signal::{self, Signal},
    unistd::Pid,
};
use signal_hook::{iterator::Signals, SIGINT, SIGTERM};
use tracing::{error, info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

// TODO: give unique error for every case
fn main() -> Result<(), Error> {
    block_on(async_main())
}

// TODO: 2 components (broker watchdog / edgehub watchdog)
// each component runs separate thread.
// in separate thread: start process and while loop to check for process status and tell main process to exit
// use channels to communicate between threads
async fn async_main() -> Result<(), Error> {
    init_logging();
    info!("Starting watchdog");

    let should_shutdown = match register_sigterm_listener() {
        Ok(should_shutdown) => should_shutdown,
        Err(e) => {
            error!(
                "Failed to register sigterm listener. Shutting down. {:?}",
                e
            );
            exit(1);
        }
    };

    // start edgehub and blow up if can't start
    let mut edgehub = match Command::new("dotnet")
        .arg("/app/Microsoft.Azure.Devices.Edge.Hub.Service.dll")
        .stdout(Stdio::inherit())
        .spawn()
    {
        Ok(edgehub) => {
            info!("Launched Edge Hub process with pid {:?}", edgehub.id());
            edgehub
        }
        Err(e) => {
            error!("Failed to start Edge Hub process {:?}", e);
            exit(1);
        }
    };

    // unwrap broker if started, else shutdown edgehub and exit
    let mut broker = match Command::new("/usr/local/bin/mqttd")
        .stdout(Stdio::inherit())
        .spawn()
    {
        Ok(child) => child,
        Err(e) => {
            info!(
                "Broker process failed to start, so shutting down EdgeHub and exiting. {:?}",
                e
            );
            shutdown(&mut edgehub).await;
            exit(1);
        }
    };
    info!("Launched MQTT Broker process");

    while !should_shutdown.load(Ordering::Relaxed) {
        if !is_running(&mut edgehub) {
            break;
        }

        if !is_running(&mut broker) {
            break;
        }

        thread::sleep(Duration::from_secs(1));
    }

    let shutdown_edgehub = shutdown(&mut edgehub);
    let shutdown_broker = shutdown(&mut broker);
    join!(shutdown_edgehub, shutdown_broker);

    info!("Stopped");
    Ok(())
}

fn init_logging() {
    let subscriber = Subscriber::builder().with_max_level(Level::INFO).finish();
    let _ = subscriber::set_global_default(subscriber);
}

fn register_sigterm_listener() -> Result<Arc<AtomicBool>, Error> {
    // TODO: figure out how to log shutdown signal
    // TODO: figure out how to consolidate signal hook
    // TODO: do we receive signal that happened before listener registered
    let should_shutdown = Arc::new(AtomicBool::new(false));
    signal_hook::flag::register(signal_hook::SIGTERM, Arc::clone(&should_shutdown))?;
    signal_hook::flag::register(signal_hook::SIGINT, Arc::clone(&should_shutdown))?;
    Ok(should_shutdown)
}

fn is_running(child_process: &mut Child) -> bool {
    match child_process.try_wait() {
        Ok(status) => status.is_none(),
        Err(e) => {
            error!("Error while polling child process. {:?}", e);
            false
        }
    }
}

// TODO: new logic to send shutdown signal to both processes
// TODO: convert 60 second straight wait to be cancelled
async fn shutdown(mut child: &mut Child) {
    if is_running(&mut child) {
        info!("Terminating child process");
        terminate(&mut child);
    }

    thread::sleep(Duration::from_secs(60));

    if is_running(&mut child) {
        info!("Killing child process");
        kill(child);
    }
}

fn terminate(child: &mut Child) {
    if let Err(e) = signal::kill(Pid::from_raw(child.id() as i32), Signal::SIGTERM) {
        error!("Failed to send SIGTERM signal to child process. {:?}", e);
    }
}

fn kill(child: &mut Child) {
    if let Err(e) = child.kill() {
        error!("Failed to kill child process. {:?}", e);
    }
}
