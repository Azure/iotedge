// Copyright (c) Microsoft. All rights reserved.

use std::sync::Arc;
use std::path::Path;

extern crate zip;
use std::io::{Write, Seek};

use futures::{Future, Stream};
use tokio::prelude::*;

use crate::error::{Error, ErrorKind};
use failure::Fail;

use crate::logs::pull_logs;
use crate::Command;

use edgelet_core::{LogOptions, Module, ModuleRuntime};

pub struct Bundle<M, W>
where W: 'static + Write + Seek + Send
 {
    state: Arc<BundleState<M>>,
    writer: W,
}

struct BundleState<M>
{
    runtime: M,
    log_options: LogOptions,
    location: String,
    file_options: zip::write::FileOptions,
}

impl<M, W> Bundle<M, W>
where W: 'static + Write + Seek + Send
{
    pub fn new(log_options: LogOptions, location: &str, runtime: M, writer: W) -> Self {
        let state = BundleState {
                runtime,
                log_options,
                location: location.to_owned(),
                file_options: zip::write::FileOptions::default()
                    .compression_method(zip::CompressionMethod::Deflated)
            };
        Bundle {
            state: Arc::new(state),
                            writer,
        }
    }
}

impl<M, W> Command for Bundle<M, W>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
    W: 'static + Write + Seek + Send + Sync,
{
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(&mut self) -> Self::Future {
                let mut zip_writer = zip::ZipWriter::new(self.writer);

        let dir = format!("{}/bundle/logs", self.state.location);
        let dir_path = Path::new(&dir);
        if zip_writer.add_directory_from_path(&dir_path, self.state.file_options).is_err() {
            // TODO: make error kind
            return Box::new(future::err(Error::from(ErrorKind::WriteToStdout)));
        }

        let state = self.state.clone();
        let result = state.get_modules()
            .and_then(move |names| {
                stream::iter_ok(names).fold(zip_writer, move |zw, name| state.write_log_to_file(zw, name))
            })
            .map(drop);

        Box::new(result)
    }
}

impl<M> BundleState<M>
where
    M: 'static + ModuleRuntime + Clone,
{
    fn get_modules(&self) -> impl Future<Item = Vec<String>, Error = Error> {
        self.runtime
            .list_with_details()
            .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            .map(|(module, _s)| module.name().to_string())
            .collect()
    }

    fn write_log_to_file<W>(&self, zip_writer: zip::ZipWriter<W>, module_name: String) -> impl Future<Item = zip::ZipWriter<W>, Error = Error>
    where W: 'static + Write + Seek + Send,
    {
        println!("Writing {} to file", module_name);
        let file_name = format!("{}/bundle/logs/{}_log.txt", self.location, module_name);

        pull_logs(&self.runtime, &module_name, &self.log_options, zip_writer)
    }
}
