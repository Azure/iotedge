// Copyright (c) Microsoft. All rights reserved.

use crate::logs::pull_logs;
use futures::future::Future;
use futures::prelude::*;
use std::io::{self};

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

        let log1 = pull_logs(&self.runtime, &id, &log_options, io::stdout());

        log1
    }
}
