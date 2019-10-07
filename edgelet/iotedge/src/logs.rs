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

    fn execute(self) -> Self::Future {
        let id = self.id.clone();
        let result = pull_logs(&self.runtime, &id, &self.options, io::stdout()).map(drop);
        Box::new(result)
    }
}

pub fn pull_logs<M, W>(
    runtime: &M,
    id: &str,
    options: &LogOptions,
    writer: W,
) -> impl Future<Item = W, Error = Error> + Send
where
    M: 'static + ModuleRuntime,
    W: 'static + Write + Send,
{
    runtime
        .logs(id, options)
        .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
        .and_then(move |logs| {
            let chunked =
                Chunked::new(logs.map_err(|_| io::Error::new(io::ErrorKind::Other, "unknown")));
            LogDecode::new(chunked)
                .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
                .fold(writer, |mut w, chunk| -> Result<W, Error> {
                    match chunk {
                        LogChunk::Stdin(b)
                        | LogChunk::Stdout(b)
                        | LogChunk::Stderr(b)
                        | LogChunk::Unknown(b) => w
                            .write(&b)
                            .map_err(|err| Error::from(err.context(ErrorKind::WriteToStdout)))?,
                    };
                    Ok(w)
                })
        })
}
