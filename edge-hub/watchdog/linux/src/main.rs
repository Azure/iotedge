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
use signal_hook::{iterator::Signals, SIGINT, SIGTERM};
use tracing::{error, info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

const PROCESS_SHUTDOWN_TOLERANCE: Duration = Duration { secs: 60, nanos: 0 };
const PROCESS_POLL_INTERVAL: Duration = Duration { secs: 1, nanos: 0 };

struct ChildProcess {
    pub name: String,
    pub child: Child,
    pub has_process_exited: Arc<AtomicBool>,
}

// TODO: 2 components (broker / edgehub)
// each component runs separate thread.
// in separate thread: start process and while loop to check for process status and tell main process to exit
// use channels to communicate between threads
impl ChildProcess {
    pub fn make_child_process(name: String, command: &mut Command) -> ChildProcess {
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
        let has_process_exited = Self::register_shutdown_listener(&mut child);

        ChildProcess {
            name: name,
            child: child,
            has_process_exited: has_process_exited,
        }
    }

    pub fn trigger_shutdown(self) {
        thread::spawn(move || {
            if Self::is_running(&mut self.child) {
                info!("Terminating {:?}", self.name);
                self.send_signal(Signal::SIGTERM);
            }

            self.wait_for_exit();

            if Self::is_running(&mut self.child) {
                info!("Killing {:?}", self.name);
                self.send_signal(Signal::SIGKILL);
            }
        });
    }

    fn register_shutdown_listener(child: &mut Child) -> Arc<AtomicBool> {
        let mut has_shutdown = Arc::new(AtomicBool::new(false));
        let has_shutdown_listener = Arc::clone(&mut has_shutdown);
        thread::spawn(move || {
            while Self::is_running(&mut child) {
                thread::sleep(PROCESS_POLL_INTERVAL);
            }
            has_shutdown_listener.store(true, Ordering::Relaxed);
        });
        return has_shutdown;
    }

    // TODO: get signal name and log it
    fn send_signal(self, signal: Signal) {
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

    fn wait_for_exit(self) {
        let mut elapsed_secs = 0;
        while elapsed_secs < PROCESS_SHUTDOWN_TOLERANCE.as_secs()
            && Self::is_running(&mut self.child)
        {
            thread::sleep(PROCESS_POLL_INTERVAL);

            elapsed_secs += PROCESS_POLL_INTERVAL.as_secs();
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

    let mut broker = ChildProcess::make_child_process(
        "MQTT Broker".to_string(),
        Command::new("/usr/local/bin/mqttd").stdout(Stdio::inherit()),
    );

    let mut edgehub = ChildProcess::make_child_process(
        "Edge Hub".to_string(),
        Command::new("dotnet")
            .arg("/app/Microsoft.Azure.Devices.Edge.Hub.Service.dll")
            .stdout(Stdio::inherit()),
    );

    while !should_shutdown.load(Ordering::Relaxed) {
        if broker.has_process_exited.load(Ordering::Relaxed) {
            break;
        }

        if edgehub.has_process_exited.load(Ordering::Relaxed) {
            break;
        }

        thread::sleep(Duration::from_secs(1));
    }

    if !broker.has_process_exited.load(Ordering::Relaxed) {
        broker.trigger_shutdown();
    }

    if !edgehub.has_process_exited.load(Ordering::Relaxed) {
        edgehub.trigger_shutdown();
    }

    // Note: this func doesn't need to happen in a separate thread because both don't need to run at the same time
    broker.wait_for_exit();
    edgehub.wait_for_exit();

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
    signal_hook::flag::register(signal_hook::SIGTERM, Arc::clone(&should_shutdown))?;
    signal_hook::flag::register(signal_hook::SIGINT, Arc::clone(&should_shutdown))?;
    Ok(should_shutdown)
}
