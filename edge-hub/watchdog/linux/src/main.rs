// questions for denis:

// have i structured this struct correctly
// is it ok to pass arc atomic bools from worker threads handling child process to main thread, and use atomic bools to talk to worker threads

// command issue - because it gets created initially as a mutable reference, it would be easier to create this inside the thread.
// so from main what if we just pass the information necessary to start the command

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
    pub child: Child,
}

// TODO: 2 components (broker / edgehub)
// each component runs separate thread.
// in separate thread: start process and while loop to check for process status and tell main process to exit
// use channels to communicate between threads
// TODO: find documentation standards for func param
impl ChildProcess {
    pub fn run_child_process(
        name: String,
        command: &mut Command,
        parent_shutdown_listener: Arc<AtomicBool>,
    ) -> Arc<AtomicBool> {
        // start child process before spawning new thread because it will get freed after the caller runs this function
        // this makes lifetimes easier to deal with
        let child = match command.spawn() {
            Ok(child) => {
                info!("Launched {:?} with pid {:?}", name, child.id());
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
                child: child,
            };

            while Self::is_running(&mut child_process.child)
                && parent_shutdown_listener.load(Ordering::Relaxed)
            {
                let poll_interval: Duration = Duration::new(PROCESS_POLL_INTERVAL_SECS, 0);
                thread::sleep(poll_interval);
            }

            if Self::is_running(&mut child_process.child) {
                child_process.trigger_shutdown();
            } else {
                info!(
                    "Child process {:?} not running so won't attempt termination.",
                    child_process.name
                )
            }

            has_shutdown_listener.store(true, Ordering::Relaxed);
        });

        has_shutdown
    }

    fn trigger_shutdown(&mut self) {
        if Self::is_running(&mut self.child) {
            info!("Terminating {:?}", self.name);
            self.send_signal(Signal::SIGTERM);
        }

        self.wait_for_exit();

        if Self::is_running(&mut self.child) {
            info!("Killing {:?}", self.name);
            self.send_signal(Signal::SIGKILL);
        }
    }

    // TODO: get signal name and log it
    fn send_signal(&mut self, signal: Signal) {
        if let Err(e) = signal::kill(Pid::from_raw(self.child.id() as i32), signal) {
            error!("Failed to send signal to {:?}. {:?}", self.name, e);
        }
    }

    fn is_running(child: &mut Child) -> bool {
        match child.try_wait() {
            Ok(status) => status.is_none(),
            Err(e) => {
                error!("Error while polling child process status. {:?}", e);
                false
            }
        }
    }

    fn wait_for_exit(&mut self) {
        let mut elapsed_secs = 0;
        while elapsed_secs < PROCESS_SHUTDOWN_TOLERANCE_SECS && Self::is_running(&mut self.child) {
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

    // use channels to wait here and tell both worker threads to shut down (one might already be shut down)

    info!("Stopped watchdog process");
    Ok(())
}

fn init_logging() {
    let subscriber = Subscriber::builder().with_max_level(Level::INFO).finish();
    let _ = subscriber::set_global_default(subscriber);
}

fn register_shutdown_listener() -> Result<Arc<AtomicBool>, Error> {
    // TODO: figure out how to log shutdown signal
    // TODO: figure out how to consolidate signal hook
    // TODO: do we receive signal that happened before listener registered
    // TODO: remove FQN
    let should_shutdown = Arc::new(AtomicBool::new(false));
    flag::register(SIGTERM, Arc::clone(&should_shutdown))?;
    flag::register(SIGINT, Arc::clone(&should_shutdown))?;
    Ok(should_shutdown)
}
