// Copyright (c) Microsoft. All rights reserved.

use std::io::stdout;

use futures::prelude::*;

use edgelet_core::{LogOptions, ModuleRuntime};
use support_bundle::write_logs;

use crate::error::{Error, ErrorKind};

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

impl<M> Logs<M>
where
    M: ModuleRuntime,
{
    pub async fn execute(self) -> Result<(), Error> {
        let id = self.id.clone();
        let result = write_logs(&self.runtime, &id, &self.options, &mut stdout())
            .await
            .map_err(|_| Error::from(ErrorKind::ModuleRuntime))?;

        Ok(())
    }
}
