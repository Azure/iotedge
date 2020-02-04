// Copyright (c) Microsoft. All rights reserved.

use futures::future::{self, FutureResult};

use crate::error::Error;
use crate::Command;

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

    fn execute(self) -> Self::Future {
        eprintln!("unknown command: {}", self.command);
        future::ok(())
    }
}
