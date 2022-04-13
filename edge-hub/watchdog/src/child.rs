use std::{
    process::{Child, Command, Stdio},
    sync::{
        atomic::{AtomicBool, Ordering},
        Arc,
    },
    thread::{self, JoinHandle},
    time::Duration,
};

use anyhow::{Context, Result};
use nix::{
    sys::signal::{self, Signal},
    unistd::Pid,
};
use tracing::{error, info};

const PROCESS_SHUTDOWN_TOLERANCE: Duration = Duration::from_secs(60);
const PROCESS_POLL_INTERVAL: Duration = Duration::from_secs(1);

pub struct ChildProcess {
    name: String,
    process: Child,
}

impl ChildProcess {
    pub fn new(name: String, process: Child) -> Self {
        Self { name, process }
    }

    pub fn poll_running(&mut self) -> bool {
        match self.process.try_wait() {
            Ok(status) => status.is_none(),
            Err(e) => {
                error!("Error while polling {} process status. {}", self.name, e);
                false
            }
        }
    }

    pub fn shutdown_if_running(&mut self) -> Result<()> {
        if self.poll_running() {
            info!("Terminating {} process", self.name);
            self.send_signal(Signal::SIGTERM);
        } else {
            info!("{} process has stopped", self.name);
        }

        self.wait_for_exit()?;

        if self.poll_running() {
            info!("Killing {} process", self.name);
            self.send_signal(Signal::SIGKILL);
        }

        self.wait_for_exit()?;

        Ok(())
    }

    fn send_signal(&mut self, signal: Signal) {
        info!("Sending {} signal to {} process", signal, self.name);

        // Casting process.id() from u32 to i32 cannot wrap.
        // The max pid for linux is ~4 million. The max value for i32 is 2,147,483,647.
        // Linux max pid:
        // https://github.com/torvalds/linux/blob/0bddd227f3dc55975e2b8dfa7fc6f959b062a2c7/include/linux/threads.h#L31
        // Rust i32 max value:
        // https://doc.rust-lang.org/std/i32/constant.MAX.html
        #[allow(clippy::cast_possible_wrap)]
        if let Err(e) = signal::kill(Pid::from_raw(self.process.id() as i32), signal) {
            error!("Failed to send signal to {} process. {}", self.name, e);
        }
    }

    fn wait_for_exit(&mut self) -> Result<()> {
        let mut elapsed_secs = 0;
        while elapsed_secs < PROCESS_SHUTDOWN_TOLERANCE.as_secs() && self.poll_running() {
            sleep(PROCESS_POLL_INTERVAL)?;
            elapsed_secs += PROCESS_POLL_INTERVAL.as_secs();
        }

        Ok(())
    }
}

pub fn run(
    name: impl Into<String>,
    program: impl Into<String>,
    args: Vec<String>,
    should_shutdown: Arc<AtomicBool>,
) -> Result<JoinHandle<Result<()>>> {
    let name = name.into();

    let child = Command::new(program.into())
        .args(args)
        .stdout(Stdio::inherit())
        .spawn()
        .with_context(|| format!("Failed to start {:?} process.", name))?;

    let handle = thread::spawn(move || {
        info!("Launched {} process with pid {}", name, child.id());

        let mut child_process = ChildProcess::new(name, child);

        while child_process.poll_running() && !should_shutdown.load(Ordering::Relaxed) {
            sleep(PROCESS_POLL_INTERVAL)?;
        }

        // tell the threads to shut down their child process
        should_shutdown.store(true, Ordering::Relaxed);

        child_process.shutdown_if_running()?;
        Ok(())
    });
    Ok(handle)
}

// We can use `thread::sleep()` instead when this issue is resolved:
// https://github.com/rust-lang/rust/issues/95661
fn sleep(duration: Duration) -> Result<()> {
    Command::new("sleep")
        .arg(duration.as_secs().to_string())
        .output()?;

    Ok(())
}
