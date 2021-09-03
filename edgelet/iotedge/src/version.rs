// Copyright (c) Microsoft. All rights reserved.

use clap::crate_name;

#[derive(Default)]
pub struct Version;

impl Version {
    #[allow(clippy::print_literal)]
    pub fn print_version() {
        println!(
            "{} {}",
            crate_name!(),
            edgelet_core::version_with_source_version(),
        );
    }
}
