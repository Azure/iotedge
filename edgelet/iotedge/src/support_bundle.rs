// Copyright (c) Microsoft. All rights reserved.

use std::io::{copy, stdout};
use std::path::PathBuf;

use failure::Fail;
use futures::Future;

use edgelet_core::{LogOptions, ModuleRuntime};
use support_bundle::{make_bundle, OutputLocation};

use crate::error::{Error, ErrorKind};
use crate::Command;

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
    M: 'static + ModuleRuntime + Clone + Send + Sync,
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
}

impl<M> Command for SupportBundleCommand<M>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
{
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(self) -> Self::Future {
        println!("Making support bundle");

        let output_location = self.output_location.clone();
        let bundle = make_bundle(
            self.output_location,
            self.log_options,
            self.include_ms_only,
            self.verbose,
            self.iothub_hostname,
            self.runtime,
        );

        let result = bundle
            .map_err(|_| Error::from(ErrorKind::SupportBundle))
            .and_then(|(mut bundle, _size)| -> Result<(), Error> {
                match output_location {
                    OutputLocation::File(location) => {
                        let path = PathBuf::from(location);
                        println!(
                            "Created support bundle at {}",
                            path.canonicalize().unwrap_or_else(|_| path).display()
                        );

                        Ok(())
                    }
                    OutputLocation::Memory => {
                        copy(&mut bundle, &mut stdout())
                            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

                        Ok(())
                    }
                }
            });

        Box::new(result)
    }
}
