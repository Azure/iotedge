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

        // let command = {
        //     let mut command = ShellCommand::new("journalctl");
        //     command
        //         .arg("-a")
        //         .args(&["-u", "aziot-keyd"])
        //         .arg("--no-pager");

        //     command.output()
        // };

        // let (file_name, output) = if let Ok(result) = command {
        //     if result.status.success() {
        //         (format!("logs/{}.txt", name), result.stdout)
        //     } else {
        //         (format!("logs/{}_err.txt", name), result.stderr)
        //     }
        // } else {
        //     let err_message = command.err().unwrap().to_string();
        //     println!(
        //         "Could not find system logs for {}. Including error in bundle.\nError message: {}",
        //         name, err_message
        //     );
        //     (
        //         format!("logs/{}_err.txt", name),
        //         err_message.as_bytes().to_vec(),
        //     )
        // };

        Box::new(future::ok(()))
    }
}
