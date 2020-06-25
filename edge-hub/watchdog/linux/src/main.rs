use nix::sys::signal::{self, Signal};
use nix::unistd::Pid;
use signal_hook::{iterator::Signals, SIGTERM, SIGINT};
use std::io::Error;
use std::process::{Child, Command, Stdio};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::thread;
use std::time::Duration;
use tracing::{error, info, Level, subscriber};
use tracing_subscriber::fmt::Subscriber;

fn main() -> Result<(), Error> {
    init_logging();

    let has_received_shutdown_signal = match register_sigterm_listener() {
        Ok(has_received_shutdown_signal) => has_received_shutdown_signal,
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

    while has_received_shutdown_signal.load(Ordering::Relaxed) {
        if !is_running(&mut edgehub) {
            break;
        }

        if !is_running(&mut broker) {
            break;
        }

        thread::sleep(Duration::from_secs(1));
    }

    shutdown(&mut edgehub, &mut broker);

    info!("Stopped");
    Ok(())
}

fn init_logging() {
    let subscriber = Subscriber::builder()
        .with_max_level(Level::INFO)
        .finish();
    let _ = subscriber::set_global_default(subscriber);

    info!("Starting watchdog");
}

fn register_sigterm_listener() -> Result<Arc<AtomicBool>, Error> {
    let signals = Signals::new(&[SIGTERM, SIGINT])?;
    let has_received_shutdown_signal = Arc::new(AtomicBool::new(true));
    let sigterm_listener = has_received_shutdown_signal.clone();
    thread::spawn(move || {
        for _sig in signals.forever() {
            info!("Received shutdown signal for watchdog");
            sigterm_listener.store(false, Ordering::Relaxed);
        }
    });

    Ok(has_received_shutdown_signal)
}

fn is_running(child_process: &mut Child) -> bool {
    child_process.try_wait().unwrap().is_none()
}

fn shutdown(mut edgehub: &mut Child, mut broker: &mut Child) {
    info!("Initiating shutdown of MQTT Broker and Edge Hub");

    if is_running(&mut edgehub) {
        info!("Sending SIGTERM to Edge Hub");
        terminate(&mut edgehub);
    }
    if is_running(&mut broker) {
        info!("Sending SIGTERM to MQTT Broker");
        terminate(&mut broker);
    }

    thread::sleep(Duration::from_secs(60));

    if is_running(&mut edgehub) {
        info!("Killing Edge Hub");
        kill(edgehub);
    }
    if is_running(&mut broker) {
        info!("Killing MQTT Broker");
        kill(broker);
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
