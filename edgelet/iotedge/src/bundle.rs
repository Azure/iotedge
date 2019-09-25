// Copyright (c) Microsoft. All rights reserved.

use std::fs;
use std::fs::File;

use futures::{Future, Stream};

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
    M: 'static + ModuleRuntime + Clone,
{
    // type Future = FutureResult<(), Error>;
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(&mut self) -> Self::Future {
        println!("Test!");

        // let id = "edgeAgent".to_string();
        // let log_options = LogOptions::new();

        // fs::create_dir_all("./bundle/logs");
        // let file = File::create("./bundle/logs/foo.txt").unwrap();
        // let log1 = pull_logs(&self.runtime, &id, &log_options, file);

        Box::new(self.get_modules().map(|names| {
            for name in names {
                println!("{}", name);
            }
        }))
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
}
