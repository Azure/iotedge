// Copyright (c) Microsoft. All rights reserved.
#![allow(unused)]

use std::io::stdout;
use std::process::Command as ShellCommand;

use futures::prelude::*;
use futures::{Future, Stream};
use tokio::prelude::*;

use edgelet_core::{LogOptions, ModuleRuntime};

use crate::error::{Error, ErrorKind};
use crate::Command;

static PROCESSES: &[&str] = &[
    "aziot-keyd",
    "aziot-certd",
    "aziot-identityd",
    "aziot-edged",
];

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
