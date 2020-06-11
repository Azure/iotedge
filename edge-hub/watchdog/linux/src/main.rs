extern crate nix;

// TODO: standardize imports
use std::process::{Command, Stdio, Child};
use std::sync::{Arc};
use std::time::Duration;
use nix::unistd::Pid;
use nix::sys::signal::{self, Signal};
use std::{error::Error, thread};
use signal_hook::{iterator::Signals, SIGTERM};
use std::sync::atomic::{AtomicBool, Ordering};

fn shutdown(edgehub: &mut Child, broker: &mut Child) {
    println!("Initiating shutdown of broker and edgehub");

    let mut is_edgehub_stopped = edgehub.try_wait().unwrap().is_some();
    let mut is_broker_stopped = broker.try_wait().unwrap().is_some();
    if !is_edgehub_stopped {
        signal::kill(Pid::from_raw(edgehub.id() as i32), Signal::SIGTERM).unwrap();
        println!("sigterm edgehub");
    }
    if !is_broker_stopped {
        signal::kill(Pid::from_raw(broker.id() as i32), Signal::SIGTERM).unwrap();
        println!("sigterm broker");
    }
    thread::sleep(Duration::from_secs(10)); // TODO: configure wait time

    // TODO: we need to unwrap signal::kill at least to verify if end sigkill succeeded. How do we do this guaranteeing no panic from already killed process (i.e. from earlier slow sigterm)
    is_edgehub_stopped = edgehub.try_wait().unwrap().is_some();
    is_broker_stopped = broker.try_wait().unwrap().is_some();
    if !is_edgehub_stopped {
        println!("kill edgehub");
        edgehub.kill().unwrap();
    }
    if !is_broker_stopped {
        println!("kill broker");
        broker.kill().unwrap();
    }

    is_edgehub_stopped = edgehub.try_wait().unwrap().is_some();
    is_broker_stopped = edgehub.try_wait().unwrap().is_some();
    println!("edgehub stopped: {:?}", is_edgehub_stopped);
    println!("broker stopped: {:?}", is_broker_stopped);
}

fn main() -> Result<(), Box<dyn Error>> {
    // TODO: find way to clean up
    // TODO: document fact that tried to clean up duplicated stdout -> expect
    let mut edgehub = Command::new("dotnet")
            .arg("/app/Microsoft.Azure.Devices.Edge.Hub.Service.dll")
            .stdout(Stdio::inherit())
            .spawn()
            .expect("failed to execute process");
    
    let mut broker = Command::new("/usr/local/bin/mqttd")
            .stdout(Stdio::inherit())
            .spawn()
            .expect("failed to execute process");
   
    // TODO: confirm desirable to wait for edgehub and broker processes to be created first
    let signals = Signals::new(&[SIGTERM])?;
    let should_main_process_continue = Arc::new(AtomicBool::new(true));
    let should_main_process_continue_listener = should_main_process_continue.clone();
    thread::spawn(move || {
        for sig in signals.forever() {
            println!("Received signal {:?}", sig);
            should_main_process_continue_listener.store(false, Ordering::Relaxed); // TODO: figure out best ordering
        }
    });

    println!("{:?}", edgehub.id());
    println!("{:?}", broker.id());
    let mut is_edgehub_running: bool = !edgehub.try_wait().unwrap().is_some(); // TODO: extract logic into func
    let mut is_broker_running: bool = !broker.try_wait().unwrap().is_some();
    while should_main_process_continue.load(Ordering::Relaxed) && is_edgehub_running && is_broker_running {
        is_edgehub_running = !edgehub.try_wait().unwrap().is_some(); // TODO: don't define this var twice
        is_broker_running = !broker.try_wait().unwrap().is_some();

        thread::sleep(Duration::from_secs(4)); // TODO: configure wait time
    }

    shutdown(&mut edgehub, &mut broker);

    println!("Exiting");
    Ok(())
}
