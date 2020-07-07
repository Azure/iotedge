use nix::{
    sys::signal::{self, Signal},
    unistd::Pid,
};

use std::{
    process::{exit, Child, Command, Stdio},
    sync::{
        atomic::{AtomicBool, Ordering},
        Arc,
    },
    thread::{sleep, spawn, JoinHandle},
    time::Duration,
};
use tracing::{error, info};

const PROCESS_SHUTDOWN_TOLERANCE_SECS: u64 = 5;
const PROCESS_POLL_INTERVAL_SECS: u64 = 1;

pub struct ChildProcess {
    pub name: String,
    pub process: Child,
}

impl ChildProcess {
    pub fn shutdown_if_running(&mut self) {
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

    pub fn is_running(&mut self) -> bool {
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
