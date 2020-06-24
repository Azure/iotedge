extern crate nix;

use std::{error::Error, thread};
use std::process::{Command, Stdio, Child};
use std::sync::{Arc};
use std::sync::atomic::{AtomicBool, Ordering};
use std::time::Duration;
use nix::unistd::Pid;
use nix::sys::signal::{self, Signal};
use signal_hook::{iterator::Signals, SIGTERM};
use tracing::{info};
use tracing_subscriber;

fn main() -> Result<(), Box<dyn Error>> {
    init_logging();

    let signals = Signals::new(&[SIGTERM])?;
    let has_received_sigterm = Arc::new(AtomicBool::new(true));
    let sigterm_listener = has_received_sigterm.clone();
    thread::spawn(move || {
        for _sig in signals.forever() {
            info!("Received SIGTERM for watchdog");
            sigterm_listener.store(false, Ordering::Relaxed);
        }
    });

    let mut edgehub = Command::new("dotnet")
            .arg("/app/Microsoft.Azure.Devices.Edge.Hub.Service.dll")
            .stdout(Stdio::inherit())
            .spawn()
            .expect("failed to execute Edge Hub process");
    
    let mut broker = Command::new("/usr/local/bin/mqttd")
            .stdout(Stdio::inherit())
            .spawn()
            .expect("failed to execute MQTT broker process");

    info!("Launched Edge Hub process with pid {:?}", edgehub.id());
    info!("Launched MQTT Broker process with pid {:?}", broker.id());
    let mut is_edgehub_running = is_child_process_running(&mut edgehub);
    let mut  is_broker_running = is_child_process_running(&mut broker);
    while has_received_sigterm.load(Ordering::Relaxed) && is_edgehub_running && is_broker_running {
        is_edgehub_running = is_child_process_running(&mut edgehub);
        is_broker_running = is_child_process_running(&mut broker);

        thread::sleep(Duration::from_secs(4));
    }

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

fn is_child_process_running(child_process: &mut Child) -> bool
{
    return !child_process.try_wait().unwrap().is_some();
}

fn shutdown(mut edgehub: &mut Child, mut broker: &mut Child) {
    info!("Initiating shutdown of MQTT Broker and Edge Hub");

    let mut is_edgehub_running = is_child_process_running(&mut edgehub);
    let mut is_broker_running = is_child_process_running(&mut broker);
    if is_edgehub_running {
        info!("Sending SIGTERM to Edge Hub");
        signal::kill(Pid::from_raw(edgehub.id() as i32), Signal::SIGTERM).unwrap();
    }
    if is_broker_running {
        info!("Sending SIGTERM to MQTT Broker");
        signal::kill(Pid::from_raw(broker.id() as i32), Signal::SIGTERM).unwrap();
    }

    thread::sleep(Duration::from_secs(10));

    is_edgehub_running = is_child_process_running(&mut edgehub);
    is_broker_running = is_child_process_running(&mut broker);
    if is_edgehub_running {
        info!("Killing Edge Hub");
        edgehub.kill().unwrap();
    }
    if !is_broker_running {
        info!("Killing MQTT Broker");
        broker.kill().unwrap();
    }
}

