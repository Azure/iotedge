// Copyright (c) Microsoft. All rights reserved.

use std::fs::File;
use std::path::Path;

extern crate zip;

use futures::{Future, Stream};
use tokio::prelude::*;

use crate::error::{Error, ErrorKind};
use failure::Fail;

use crate::logs::pull_logs;
use crate::Command;

use edgelet_core::{LogOptions, Module, ModuleRuntime};

pub struct Bundle<M> {
    runtime: M,
    log_options: Option<LogOptions>,
    location: Option<String>,
}

struct BundleState<M> {
    runtime: M,
    log_options: LogOptions,
    location: String,
    file_options: zip::write::FileOptions,
    zip_writer: zip::ZipWriter<File>,
}

impl<M> Bundle<M>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
{
    pub fn new(log_options: LogOptions, location: &str, runtime: M) -> Self {
        Bundle {
            runtime,
            log_options: Option::Some(log_options),
            location: Option::Some(location.to_owned()),
        }
    }

    fn make_state(self) -> Result<BundleState<M>, Error> {
        if let (Some(log_options), Some(location)) = (self.log_options, self.location) {
            let file_options = zip::write::FileOptions::default()
                .compression_method(zip::CompressionMethod::Deflated);

            let mut zip_writer = zip::ZipWriter::new(
                File::create(location.to_owned())
                    .map_err(|err| Error::from(err.context(ErrorKind::WriteToStdout)))?,
            );

            zip_writer
                .add_directory_from_path(
                    &Path::new(&format!("{}/bundle/logs", location.to_owned())),
                    file_options,
                )
                .map_err(|err| Error::from(err.context(ErrorKind::WriteToStdout)))?;

            Ok(BundleState {
                runtime: self.runtime.clone(),
                log_options,
                location,
                file_options,
                zip_writer,
            })
        } else {
            Err(Error::from(ErrorKind::BadHostParameter))
        }
    }
}

impl<M> Command for Bundle<M>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
{
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(self) -> Self::Future {
        let result = future::result(self.make_state())
            .and_then(Bundle::write_all_logs)
            .map(drop);

        Box::new(result)

        // Box::new(future::ok(()))
    }
}

impl<M> Bundle<M>
where
    M: 'static + ModuleRuntime + Clone,
{
    fn write_all_logs(s1: BundleState<M>) -> impl Future<Item = BundleState<M>, Error = Error> {
        Bundle::get_modules(s1).and_then(|(names, s2)| {
            stream::iter_ok(names).fold(s2, |s3, name| Bundle::write_log_to_file(s3, name))
        })
    }

    fn get_modules(
        state: BundleState<M>,
    ) -> impl Future<Item = (Vec<String>, BundleState<M>), Error = Error> {
        state
            .runtime
            .list_with_details()
            .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            .map(|(module, _s)| module.name().to_string())
            .collect()
            .map(|names| (names, state))
    }

    fn write_log_to_file(
        state: BundleState<M>,
        module_name: String,
    ) -> impl Future<Item = BundleState<M>, Error = Error> {
        println!("Writing {} to file", module_name);
        let BundleState {
            runtime,
            log_options,
            location,
            file_options,
            mut zip_writer,
        } = state;

        let file_name = format!("{}/bundle/logs/{}_log.txt", location, module_name);
        zip_writer
            .start_file_from_path(&Path::new(&file_name), file_options)
            .into_future()
            .map_err(|err| Error::from(err.context(ErrorKind::WriteToStdout)))
            .and_then(move |_| {
                pull_logs(&runtime, &module_name, &log_options, zip_writer).map(move |zw| {
                    BundleState {
                        runtime,
                        log_options,
                        location,
                        file_options,
                        zip_writer: zw,
                    }
                })
            })
    }
}
