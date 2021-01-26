// Copyright (c) Microsoft. All rights reserved.
#![allow(unused)]

use std::io::{self, Write};
use std::process::Command as ShellCommand;

use futures::Future;
use tokio::prelude::*;

use crate::error::Error;
use crate::system::PROCESSES;
use crate::Command;

pub struct SystemRestart {}

impl Default for SystemRestart {
    fn default() -> Self {
        Self {}
    }
}

impl Command for SystemRestart {
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(self) -> Self::Future {
        for process in PROCESSES.iter().rev() {
            print!("Stopping {}...", process);
            let result = ShellCommand::new("systemctl")
                .args(&["stop", process])
                .output()
                .unwrap();

            if result.status.success() {
                println!("Stopped!");
            } else {
                println!("\nError stopping {}", process);
                io::stdout().write_all(&result.stdout).unwrap();
                io::stderr().write_all(&result.stderr).unwrap();
                println!();
            }
        }

        println!();

        for process in PROCESSES {
            print!("Starting {}...", process);
            let result = ShellCommand::new("systemctl")
                .args(&["start", process])
                .output()
                .unwrap();

            if result.status.success() {
                println!("Started!");
            } else {
                println!("\nError starting {}", process);
                io::stdout().write_all(&result.stdout).unwrap();
                io::stderr().write_all(&result.stderr).unwrap();
                println!();
            }
        }

        Box::new(future::ok(()))
    }
}
