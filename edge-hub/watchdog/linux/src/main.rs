extern crate nix;

use nix::sys::signal::{self, Signal};
use nix::unistd::Pid;
use signal_hook::{iterator::Signals, SIGTERM};
use std::io::Error;
use std::process::{Child, Command, Stdio};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::thread;
use std::time::Duration;
use tracing::{error, info};
use tracing_subscriber;

fn main() -> Result<(), Error> {
    init_logging();

    let has_received_sigterm = match register_sigterm_listener() {
        Ok(has_received_sigterm) => has_received_sigterm,
        Err(e) => {
            error!("Failed to register sigterm listener. Shutting down.");
            return Err(e);
        }
    };

    let mut edgehub = Command::new("dotnet")
        .arg("/app/Microsoft.Azure.Devices.Edge.Hub.Service.dll")
        .stdout(Stdio::inherit())
        .spawn()
        .expect("Failed to execute Edge Hub process");
    info!("Launched Edge Hub process with pid {:?}", edgehub.id());

    let mut broker = Command::new("/usr/local/bin/mqttd")
        .stdout(Stdio::inherit())
        .spawn()
        .expect("failed to execute MQTT broker process");
    info!("Launched MQTT Broker process with pid {:?}", broker.id());

    wait_for_sigterm_or_processes_to_shutdown(&mut edgehub, &mut broker, has_received_sigterm);

    shutdown(&mut edgehub, &mut broker);

    info!("Exiting");
    Ok(())
}

fn init_logging() {
    let subscriber = tracing_subscriber::fmt::Subscriber::builder()
        .with_max_level(tracing::Level::INFO)
        .finish();
    let _ = tracing::subscriber::set_global_default(subscriber);

    info!("Starting watchdog");
}

fn register_sigterm_listener() -> Result<Arc<std::sync::atomic::AtomicBool>, Error> {
    let signals = Signals::new(&[SIGTERM])?;
    let has_received_sigterm = Arc::new(AtomicBool::new(true));
    let sigterm_listener = has_received_sigterm.clone();
    thread::spawn(move || {
        for _sig in signals.forever() {
            info!("Received SIGTERM for watchdog");
            sigterm_listener.store(false, Ordering::Relaxed);
        }
    });

    Ok(has_received_sigterm)
}

fn wait_for_sigterm_or_processes_to_shutdown(
    mut edgehub: &mut Child,
    mut broker: &mut Child,
    has_received_sigterm: Arc<std::sync::atomic::AtomicBool>,
) {
    let mut is_edgehub_running = is_child_process_running(&mut edgehub);
    let mut is_broker_running = is_child_process_running(&mut broker);
    while has_received_sigterm.load(Ordering::Relaxed) && is_edgehub_running && is_broker_running {
        is_edgehub_running = is_child_process_running(&mut edgehub);
        is_broker_running = is_child_process_running(&mut broker);

        thread::sleep(Duration::from_secs(1));
    }
}

fn is_child_process_running(child_process: &mut Child) -> bool {
    return !child_process.try_wait().unwrap().is_some();
}

fn shutdown(mut edgehub: &mut Child, mut broker: &mut Child) {
    info!("Initiating shutdown of MQTT Broker and Edge Hub");

    let mut is_edgehub_running = is_child_process_running(&mut edgehub);
    let mut is_broker_running = is_child_process_running(&mut broker);
    if is_edgehub_running {
        info!("Sending SIGTERM to Edge Hub");
        sigterm_child_process(&mut edgehub);
    }
    if is_broker_running {
        info!("Sending SIGTERM to MQTT Broker");
        sigterm_child_process(&mut broker);
    }

    thread::sleep(Duration::from_secs(60));

    is_edgehub_running = is_child_process_running(&mut edgehub);
    is_broker_running = is_child_process_running(&mut broker);
    if is_edgehub_running {
        info!("Killing Edge Hub");
        kill_child_process(edgehub);
    }
    if !is_broker_running {
        info!("Killing MQTT Broker");
        kill_child_process(broker);
    }
}

fn sigterm_child_process(child: &mut Child) {
    match signal::kill(Pid::from_raw(child.id() as i32), Signal::SIGTERM) {
        Ok(_) => {}
        Err(e) => {
            error!("Failed to send SIGTERM signal to child process. {:?}", e);
        }
    };
}

fn kill_child_process(child: &mut Child) {
    match child.kill() {
        Ok(_) => {}
        Err(e) => {
            error!("Failed to kill child process. {:?}", e);
        }
    };
}
