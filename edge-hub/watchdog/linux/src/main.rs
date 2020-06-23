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

const LOG_HEADER: &'static str = "[WATCHDOG]:";

fn main() -> Result<(), Box<dyn Error>> {
    let mut edgehub = Command::new("dotnet")
            .arg("/app/Microsoft.Azure.Devices.Edge.Hub.Service.dll")
            .stdout(Stdio::inherit())
            .spawn()
            .expect("failed to execute Edge Hub process");
    
    let mut broker = Command::new("/usr/local/bin/mqttd")
            .stdout(Stdio::inherit())
            .spawn()
            .expect("failed to execute MQTT broker process");
   
    let signals = Signals::new(&[SIGTERM])?;
    let has_received_sigterm = Arc::new(AtomicBool::new(true));
    let sigterm_listener = has_received_sigterm.clone();
    thread::spawn(move || {
        for _sig in signals.forever() {
            println!("{:?} Received SIGTERM for watchdog", LOG_HEADER);
            sigterm_listener.store(false, Ordering::Relaxed);
        }
    });

    println!("{:?} launched Edge Hub process with pid {:?}", LOG_HEADER, edgehub.id());
    println!("{:?} launched MQTT Broker process with pid {:?}", LOG_HEADER, broker.id());
    let mut is_edgehub_running = is_child_process_running(&mut edgehub);
    let mut  is_broker_running = is_child_process_running(&mut broker);
    while has_received_sigterm.load(Ordering::Relaxed) && is_edgehub_running && is_broker_running {
        is_edgehub_running = is_child_process_running(&mut edgehub);
        is_broker_running = is_child_process_running(&mut broker);

        thread::sleep(Duration::from_secs(4));
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

    thread::sleep(Duration::from_secs(10));

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

