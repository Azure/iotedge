// Copyright (c) Microsoft. All rights reserved.

use crate::logs::pull_logs;

use std::fs::File;
use std::fs;

use futures::future::Future;

use crate::error::{Error};
use crate::Command;

use edgelet_core::{LogOptions, ModuleRuntime};

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
        let id = "edgeAgent".to_string();
        let log_options = LogOptions::new();

        fs::create_dir_all("./bundle/logs");

        let mut file = File::create("./bundle/logs/foo.txt").unwrap();

        let log1 = pull_logs(&self.runtime, &id, &log_options, file);

        log1
    }
}
