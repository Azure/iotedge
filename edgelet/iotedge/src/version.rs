// Copyright (c) Microsoft. All rights reserved.

use edgelet_core;
use futures::future::{self, FutureResult};

use error::Error;
use Command;

#[derive(Default)]
pub struct Version;

impl Version {
    pub fn new() -> Self {
        Version
    }
}

impl Command for Version {
    type Future = FutureResult<(), Error>;

    #[cfg_attr(feature = "cargo-clippy", allow(print_literal))]
    fn execute(&mut self) -> Self::Future {
        println!("{} {}", crate_name!(), edgelet_core::version());
        future::ok(())
    }
}
