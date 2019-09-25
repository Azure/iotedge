// Copyright (c) Microsoft. All rights reserved.

use std::fs;
use std::fs::File;

use futures::{Future, Stream};
use tokio::prelude::*;

use crate::error::{Error, ErrorKind};
use failure::Fail;

use crate::logs::pull_logs;
use crate::Command;

use edgelet_core::{LogOptions, Module, ModuleRuntime};

pub struct Bundle<M> {
    runtime: M,
}

impl<M> Bundle<M> {
    pub fn new(runtime: M) -> Self {
        Bundle { runtime }
    }
}

impl<M> Command for Bundle<M>
where
    M: 'static + ModuleRuntime + Clone + Send,
{
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(&mut self) -> Self::Future {
        println!("Test!");

        if fs::create_dir_all("./bundle/logs").is_err() {
            // TODO: make error kind
            return Box::new(future::err(Error::from(ErrorKind::WriteToStdout)));
        }

        let runtime = self.runtime.clone();

        let result = self
            .get_modules()
            .and_then(move |names| {
                future::join_all(
                    names
                        .into_iter()
                        .map(move |name| Bundle::write_log_to_file(runtime.clone(), name)),
                )
            })
            .map(drop);

        Box::new(result)
    }
}

impl<M> Bundle<M>
where
    M: 'static + ModuleRuntime + Clone,
{
    fn get_modules(&self) -> impl Future<Item = Vec<String>, Error = Error> {
        self.runtime
            .list_with_details()
            .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            .map(|(module, _state)| module.name().to_string())
            .collect()
    }

    fn write_log_to_file(runtime: M, module_name: String) -> impl Future<Item = (), Error = Error> {
        println!("Writing {} to file", module_name);
        let log_options = LogOptions::new();
        let file_name = format!("./bundle/logs/{}_log.txt", module_name);

        future::result(File::create(file_name))
            .map_err(|err| Error::from(err.context(ErrorKind::WriteToStdout)))
            .and_then(move |file| pull_logs(&runtime, &module_name, &log_options, file))
    }
}
