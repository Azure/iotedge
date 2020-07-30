// Copyright (c) Microsoft. All rights reserved.

use std::io::stdout;

use futures::prelude::*;

use edgelet_core::{LogOptions, ModuleRuntime};
use support_bundle::pull_logs;

use crate::error::{Error, ErrorKind};
use crate::Command;

pub struct Logs<M> {
    id: String,
    options: LogOptions,
    runtime: M,
}

impl<M> Logs<M> {
    pub fn new(id: String, options: LogOptions, runtime: M) -> Self {
        Logs {
            id,
            options,
            runtime,
        }
    }
}

impl<M> Command for Logs<M>
where
    M: 'static + ModuleRuntime + Clone,
{
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(self) -> Self::Future {
        let id = self.id.clone();
        let result = pull_logs(&self.runtime, &id, &self.options, stdout())
            .map_err(|_| Error::from(ErrorKind::ModuleRuntime))
            .map(drop);
        Box::new(result)
    }
}
