// Copyright (c) Microsoft. All rights reserved.

use std::process::Command as ShellCommand;

use futures::Future;
use tokio::prelude::*;

use crate::error::Error;
use crate::system::PROCESSES;
use crate::Command;

pub struct SystemLogs {
    options: Vec<String>,
}

impl SystemLogs {
    pub fn new(options: Vec<String>) -> Self {
        Self { options }
    }
}

impl Command for SystemLogs {
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(self) -> Self::Future {
        let processes = PROCESSES.iter().flat_map(|p| vec!["-u", p]);

        ShellCommand::new("journalctl")
            .args(processes)
            .args(self.options)
            .spawn()
            .unwrap()
            .wait()
            .unwrap();

        Box::new(future::ok(()))
    }
}
