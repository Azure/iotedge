use futures::executor::block_on;
use futures::join;
use nix::sys::signal::{self, Signal};
use nix::unistd::Pid;
use signal_hook::{iterator::Signals, SIGINT, SIGTERM};
use std::io::Error;
use std::process::{exit, Child, Command, Stdio};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::thread;
use std::time::Duration;
use tracing::{error, info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

fn main() -> Result<(), Error> {
    block_on(async_main())
}

async fn async_main() -> Result<(), Error> {
    init_logging();
    info!("Starting watchdog");

    let should_shutdown = match register_sigterm_listener() {
        Ok(should_shutdown) => should_shutdown,
        Err(e) => {
            error!("Failed to register sigterm listener. Shutting down.");
            return Err(e);
        }
    };

    // start edgehub and blow up if can't start
    let mut edgehub = match Command::new("dotnet")
        .arg("/app/Microsoft.Azure.Devices.Edge.Hub.Service.dll")
        .stdout(Stdio::inherit())
        .spawn() {
        Ok(edgehub) => {
            info!("Launched Edge Hub process with pid {:?}", edgehub.id());
            edgehub
        },
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
            info!("Broker process failed to start, so shutting down EdgeHub and exiting. {:?}", e);
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
    let signals = Signals::new(&[SIGTERM, SIGINT])?;
    let should_shutdown = Arc::new(AtomicBool::new(false));
    let sigterm_listener = should_shutdown.clone();
    thread::spawn(move || {
        for _sig in signals.forever() {
            info!("Received shutdown signal for watchdog");
            sigterm_listener.store(true, Ordering::Relaxed);
        }
    });

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
