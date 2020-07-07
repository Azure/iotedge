use std::{
    io::Error,
    process::{exit, Child, Command, Stdio},
    sync::atomic::{AtomicBool, Ordering},
    sync::Arc,
    thread::{sleep, spawn, JoinHandle},
    time::Duration,
};

use nix::{
    sys::signal::{self, Signal},
    unistd::Pid,
};
use signal_hook::{flag::register, SIGINT, SIGTERM};
use tracing::{error, info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

const PROCESS_SHUTDOWN_TOLERANCE_SECS: u64 = 5;
const PROCESS_POLL_INTERVAL_SECS: u64 = 1;

struct ChildProcess {
    pub name: String,
    pub process: Child,
}

impl ChildProcess {
    fn shutdown_if_running(&mut self) {
        if self.is_running() {
            info!("Terminating {:?} process", self.name);
            self.send_signal(Signal::SIGTERM);
        } else {
            info!("{:?} process has stopped", self.name)
        }

        self.wait_for_exit();

        if self.is_running() {
            info!("Killing {:?} process", self.name);
            self.send_signal(Signal::SIGKILL);
        }
    }

    fn send_signal(&mut self, signal: Signal) {
        info!("Sending {:?} signal to {:?} process", signal, self.name);
        if let Err(e) = signal::kill(Pid::from_raw(self.process.id() as i32), signal) {
            error!("Failed to send signal to {:?} process. {:?}", self.name, e);
        }
    }

    fn is_running(&mut self) -> bool {
        match self.process.try_wait() {
            Ok(status) => status.is_none(),
            Err(e) => {
                error!(
                    "Error while polling {:?} process status. {:?}",
                    self.name, e
                );
                false
            }
        }
    }

    fn wait_for_exit(&mut self) {
        let mut elapsed_secs = 0;
        while elapsed_secs < PROCESS_SHUTDOWN_TOLERANCE_SECS && self.is_running() {
            let poll_interval: Duration = Duration::from_secs(PROCESS_POLL_INTERVAL_SECS);
            sleep(poll_interval);
            elapsed_secs += PROCESS_POLL_INTERVAL_SECS;
        }
    }
}

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

pub fn run_child_process(
    name: String,
    program: String,
    args: String,
    shutdown_listener: Arc<AtomicBool>,
) -> JoinHandle<()> {
    spawn(move || {
        let child = match Command::new(program)
            .arg(args)
            .stdout(Stdio::inherit())
            .spawn()
        {
            Ok(child) => {
                info!("Launched {:?} process with pid {:?}", name, child.id());
                child
            }
            Err(e) => {
                error!("Failed to start {:?} process. {:?}", name, e);
                exit(2);
            }
        };
        let mut child_process = ChildProcess {
            name,
            process: child,
        };

        while child_process.is_running() && !shutdown_listener.load(Ordering::Relaxed) {
            let poll_interval: Duration = Duration::from_secs(PROCESS_POLL_INTERVAL_SECS);
            sleep(poll_interval);
        }

        shutdown_listener.store(true, Ordering::Relaxed); // tell the threads to shut down their child process

        child_process.shutdown_if_running();
    })
}
