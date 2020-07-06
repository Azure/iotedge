use std::{
    io::Error,
    process::{exit, Child, Command, Stdio},
    sync::atomic::{AtomicBool, Ordering},
    sync::Arc,
    thread,
    time::Duration,
};

use nix::sys::signal::{self, Signal};
use nix::unistd::Pid;
use signal_hook::{flag, SIGINT, SIGTERM};
use tracing::{error, info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

const PROCESS_SHUTDOWN_TOLERANCE_SECS: u64 = 5;
const PROCESS_POLL_INTERVAL_SECS: u64 = 1;

struct ChildProcess {
    pub name: String,
    pub process: Child,
}

// TODO: find documentation standards for func param
impl ChildProcess {
    pub fn run_child_process(
        name: String,
        command: &mut Command,
        has_parent_started_shutdown: Arc<AtomicBool>,
    ) -> Arc<AtomicBool> {
        // start child process before spawning new thread because it will get freed after the caller runs this function
        // this makes lifetimes easier to deal with
        let child = match command.spawn() {
            Ok(child) => {
                info!("Launched {:?} process with pid {:?}", name, child.id());
                child
            }
            Err(e) => {
                error!("Failed to start {:?} process. {:?}", name, e);
                exit(2);
            }
        };

        let mut has_shutdown = Arc::new(AtomicBool::new(false));
        let has_shutdown_listener = Arc::clone(&mut has_shutdown);
        thread::spawn(move || {
            let mut child_process = ChildProcess {
                name: name,
                process: child,
            };

            while child_process.is_running() && !has_parent_started_shutdown.load(Ordering::Relaxed)
            {
                let poll_interval: Duration = Duration::new(PROCESS_POLL_INTERVAL_SECS, 0);
                thread::sleep(poll_interval);
            }

            if child_process.is_running() {
                child_process.shutdown_if_running();
            }

            has_shutdown_listener.store(true, Ordering::Relaxed);
        });

        has_shutdown
    }

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

    // TODO: get signal name and log it
    fn send_signal(&mut self, signal: Signal) {
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
            let poll_interval: Duration = Duration::new(PROCESS_POLL_INTERVAL_SECS, 0);
            thread::sleep(poll_interval);
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

    let has_broker_shutdown = ChildProcess::run_child_process(
        "MQTT Broker".to_string(),
        Command::new("/usr/local/bin/mqttd").stdout(Stdio::inherit()),
        Arc::clone(&should_shutdown),
    );

    let has_edgehub_shutdown = ChildProcess::run_child_process(
        "Edge Hub".to_string(),
        Command::new("dotnet")
            .arg("/app/Microsoft.Azure.Devices.Edge.Hub.Service.dll")
            .stdout(Stdio::inherit()),
        Arc::clone(&should_shutdown),
    );

    while !should_shutdown.load(Ordering::Relaxed) {
        if has_broker_shutdown.load(Ordering::Relaxed) {
            break;
        }

        if has_edgehub_shutdown.load(Ordering::Relaxed) {
            break;
        }

        thread::sleep(Duration::from_secs(1));
    }

    should_shutdown.store(true, Ordering::Relaxed); // tell the threads to shut down their child process

    // TODO: should we block on the thread exit? We don't have access to the threads intentionally though
    let mut elapsed_secs = 0;
    let buffered_wait_secs = PROCESS_SHUTDOWN_TOLERANCE_SECS + 5; // give buffer to allow child processes to be killed
    while elapsed_secs < buffered_wait_secs
        && (!has_broker_shutdown.load(Ordering::Relaxed)
            || !has_edgehub_shutdown.load(Ordering::Relaxed))
    {
        let poll_interval: Duration = Duration::new(PROCESS_POLL_INTERVAL_SECS, 0);
        thread::sleep(poll_interval);
        elapsed_secs += PROCESS_POLL_INTERVAL_SECS;
    }

    info!("Stopped watchdog process");
    Ok(())
}

fn init_logging() {
    let subscriber = Subscriber::builder().with_max_level(Level::INFO).finish();
    let _ = subscriber::set_global_default(subscriber);
}

// TODO: figure out how to log shutdown signal
fn register_shutdown_listener() -> Result<Arc<AtomicBool>, Error> {
    info!("Registering shutdown signal listener");
    let should_shutdown = Arc::new(AtomicBool::new(false));
    flag::register(SIGTERM, Arc::clone(&should_shutdown))?;
    flag::register(SIGINT, Arc::clone(&should_shutdown))?;
    Ok(should_shutdown)
}
