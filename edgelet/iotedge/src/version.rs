// Copyright (c) Microsoft. All rights reserved.

use futures::future::{self, FutureResult};

use Command;
use error::Error;

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
        println!("{} {}", crate_name!(), crate_version!());
        future::ok(())
    }
}
