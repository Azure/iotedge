// Copyright (c) Microsoft. All rights reserved.

use std::io::{self, Write};

use failure::Fail;
use futures::prelude::*;

use edgelet_core::{Chunked, LogChunk, LogDecode, LogOptions, ModuleRuntime};

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

    fn execute(&mut self) -> Self::Future {
        let id = self.id.clone();
        let result = self
            .runtime
            .logs(&id, &self.options)
            .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            .and_then(move |logs| {
                let chunked =
                    Chunked::new(logs.map_err(|_| io::Error::new(io::ErrorKind::Other, "unknown")));
                LogDecode::new(chunked)
                    .for_each(|chunk| {
                        match chunk {
                            LogChunk::Stdin(b)
                            | LogChunk::Stdout(b)
                            | LogChunk::Stderr(b)
                            | LogChunk::Unknown(b) => io::stdout().write(&b)?,
                        };
                        Ok(())
                    })
                    .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            });
        Box::new(result)
    }
}
