extern crate nix;

// TODO: standardize imports
use signal_hook::{iterator::Signals, SIGTERM};
use std::{error::Error, thread};
use std::process::{Command, Stdio, Child};
use std::sync::{Arc};
use std::sync::atomic::{AtomicBool, Ordering};
use std::time::Duration;
use nix::unistd::Pid;
use nix::sys::signal::{self, Signal};

// TODO: should we set up logging more formally with dependency
// TODO: if not, then we can add info / error
const LOG_HEADER: &'static str = "[WATCHDOG]:";

fn main() -> Result<(), Box<dyn Error>> {
    // TODO: find way to clean up
    // TODO: document fact that tried to clean up duplicated stdout -> expect
    let mut edgehub = Command::new("dotnet")
            .arg("/app/Microsoft.Azure.Devices.Edge.Hub.Service.dll")
            .stdout(Stdio::inherit())
            .spawn()
            .expect("failed to execute Edge Hub process");
    
    let mut broker = Command::new("/usr/local/bin/mqttd")
            .stdout(Stdio::inherit())
            .spawn()
            .expect("failed to execute MQTT broker process");
   
    // TODO: confirm desirable to wait for edgehub and broker processes to be created first
    let signals = Signals::new(&[SIGTERM])?;
    let has_received_sigterm = Arc::new(AtomicBool::new(true));
    let sigterm_listener = has_received_sigterm.clone();
    thread::spawn(move || {
        for _sig in signals.forever() {
            println!("{:?} Received SIGTERM for watchdog", LOG_HEADER);
            sigterm_listener.store(false, Ordering::Relaxed); // TODO: figure out best Ordering
        }
    });

    println!("{:?} launched Edge Hub process with pid {:?}", LOG_HEADER, edgehub.id());
    println!("{:?} launched MQTT Broker process with pid {:?}", LOG_HEADER, broker.id());
    let mut is_edgehub_running = is_child_process_running(&mut edgehub);
    let mut  is_broker_running = is_child_process_running(&mut broker);
    while has_received_sigterm.load(Ordering::Relaxed) && is_edgehub_running && is_broker_running {
        is_edgehub_running = is_child_process_running(&mut edgehub);
        is_broker_running = is_child_process_running(&mut broker);

        thread::sleep(Duration::from_secs(4)); // TODO: configure wait time for poll
    }

    shutdown(&mut edgehub, &mut broker);

    println!("Exiting");
    Ok(())
}

fn is_child_process_running(child_process: &mut Child) -> bool
{
    return !child_process.try_wait().unwrap().is_some();
}

fn shutdown(mut edgehub: &mut Child, mut broker: &mut Child) {
    println!("{:?} Initiating shutdown of MQTT Broker and Edge Hub", LOG_HEADER);

    let mut is_edgehub_running = is_child_process_running(&mut edgehub);
    let mut is_broker_running = is_child_process_running(&mut broker);
    if is_edgehub_running {
        println!("{:?} Sending SIGTERM to Edge Hub", LOG_HEADER);
        signal::kill(Pid::from_raw(edgehub.id() as i32), Signal::SIGTERM).unwrap();
    }
    if is_broker_running {
        println!("{:?} Sending SIGTERM to MQTT Broker", LOG_HEADER);
        signal::kill(Pid::from_raw(broker.id() as i32), Signal::SIGTERM).unwrap();
    }

    thread::sleep(Duration::from_secs(10)); // TODO: configure wait time

    // TODO: we need to unwrap signal::kill at least to verify if end sigkill succeeded. How do we do this guaranteeing no panic from already killed process (i.e. from earlier slow sigterm)
    is_edgehub_running = is_child_process_running(&mut edgehub);
    is_broker_running = is_child_process_running(&mut broker);
    if is_edgehub_running {
        println!("Killing Edge Hub");
        edgehub.kill().unwrap();
    }
    if !is_broker_running {
        println!("Killing MQTT Broker");
        broker.kill().unwrap();
    }
}

