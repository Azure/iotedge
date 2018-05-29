// Copyright (c) Microsoft. All rights reserved.

use futures::future::{self, FutureResult};

use error::Error;
use Command;

pub struct Unknown {
    command: String,
}

impl Unknown {
    pub fn new(command: String) -> Self {
        Unknown { command }
    }
}

impl Command for Unknown {
    type Future = FutureResult<(), Error>;

    fn execute(&mut self) -> Self::Future {
        eprintln!("unknown command: {}", self.command);
        future::ok(())
    }
}
