// Copyright (c) Microsoft. All rights reserved.

use std::io::{copy, stdout};
use std::path::PathBuf;

use anyhow::Context;

use edgelet_core::{LogOptions, ModuleRuntime};
use support_bundle::{make_bundle, OutputLocation};

use crate::error::Error;

pub struct SupportBundleCommand<M> {
    runtime: M,
    log_options: LogOptions,
    include_ms_only: bool,
    verbose: bool,
    iothub_hostname: Option<String>,
    output_location: OutputLocation,
}

impl<M> SupportBundleCommand<M>
where
    M: ModuleRuntime,
{
    pub fn new(
        log_options: LogOptions,
        include_ms_only: bool,
        verbose: bool,
        iothub_hostname: Option<String>,
        output_location: OutputLocation,
        runtime: M,
    ) -> Self {
        Self {
            runtime,
            log_options,
            include_ms_only,
            verbose,
            iothub_hostname,
            output_location,
        }
    }

    pub async fn execute(self) -> anyhow::Result<()> {
        println!("Making support bundle");

        let output_location = self.output_location.clone();
        let (mut bundle, _size) = make_bundle(
            self.output_location,
            self.log_options,
            self.include_ms_only,
            self.verbose,
            self.iothub_hostname,
            &self.runtime,
        )
        .await
        .context(Error::SupportBundle)?;

        match output_location {
            OutputLocation::File(location) => {
                let path = PathBuf::from(location);
                println!(
                    "Created support bundle at {}",
                    path.canonicalize().unwrap_or(path).display()
                );

                Ok(())
            }
            OutputLocation::Memory => {
                copy(&mut bundle, &mut stdout()).context(Error::SupportBundle)?;

                Ok(())
            }
        }
    }
}
