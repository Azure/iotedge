// Copyright (c) Microsoft. All rights reserved.

use clap::crate_name;

use edgelet_core;
use futures::future::{self, FutureResult};

use crate::error::Error;
use crate::Command;

#[derive(Default)]
pub struct Version;

impl Version {
    pub fn new() -> Self {
        Version
    }
}

impl Command for Version {
    type Future = FutureResult<(), Error>;

    #[allow(clippy::print_literal)]
    fn execute(self) -> Self::Future {
        println!(
            "{} {}",
            crate_name!(),
            edgelet_core::version_with_source_version(),
        );
        future::ok(())
    }
}
