use std::process::{Command};
use std::thread;
use std::time::Duration;

fn main() {
    // TODO: find way to clean up
    // TODO: document fact that tried to clean up duplicated stdout -> expect
    let mut edgehub = Command::new("powershell")
            .args(&["/C", "sh/blocking-operation.ps1"])
            .spawn()
            .expect("failed to execute process");
    
    let mut broker = Command::new("powershell")
            .args(&["/C", "sh/blocking-operation.ps1"])
            .spawn()
            .expect("failed to execute process");

    println!("{:?}", edgehub.id());
    println!("{:?}", broker.id());
    let mut should_shut_down = false;
    while !should_shut_down {
        let is_edgehub_stopped: bool = edgehub.try_wait().unwrap().is_some(); // TODO: don't define this var twice
        let is_broker_stopped: bool = broker.try_wait().unwrap().is_some();
        if is_edgehub_stopped || is_broker_stopped {
            should_shut_down = true;
            println!("One process stopped. Shutting down all processes"); // TODO: figure out if stopped process is restarted immediately
        }

        thread::sleep(Duration::from_secs(4));
    }

    // TODO: extract into function that we can then call when sigterm or sigchild
    // TODO: consolidate logic with dupicated if
    let mut is_edgehub_stopped = edgehub.try_wait().unwrap().is_some(); // TODO: get this is stopped logic into its own function
    let mut is_broker_stopped = broker.try_wait().unwrap().is_some();
    if !is_edgehub_stopped {
        println!("would sigterm edgehub but cannot");
    }
    if !is_broker_stopped {
        println!("would sigterm broker but cannot");
    }
    thread::sleep(Duration::from_secs(10));

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

    println!("Exiting");
}
