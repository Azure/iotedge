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
    options: String,
}

impl SystemLogs {
    pub fn new(options: String) -> Self {
        Self { options }
    }
}

impl Command for SystemLogs {
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(self) -> Self::Future {
        println!("Hello");

        let processes = PROCESSES.iter().flat_map(|p| vec!["-u", p]);

        let mut command = ShellCommand::new("journalctl");
        command.args(processes);

        let mut command = command.spawn().unwrap();

        command.wait().unwrap();

        Box::new(future::ok(()))
    }
}
