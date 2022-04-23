// Copyright (c) Microsoft. All rights reserved.

use std::io::{self, Write};

use anyhow::Context;
use futures::prelude::*;

use edgelet_core::{Chunked, LogChunk, LogDecode, LogOptions, ModuleRuntime};

use crate::error::Error;

pub fn pull_logs<M, W>(
    runtime: &M,
    id: &str,
    options: &LogOptions,
    writer: W,
) -> impl Future<Item = W, Error = anyhow::Error> + Send
where
    M: 'static + ModuleRuntime,
    W: Write + Send,
{
    runtime
        .logs(id, options)
        .map_err(|err| anyhow::anyhow!(err).context(Error::ModuleRuntime))
        .and_then(move |logs| {
            let chunked =
                Chunked::new(logs.map_err(|_| io::Error::new(io::ErrorKind::Other, "unknown")));
            LogDecode::new(chunked)
                .map_err(|err| anyhow::anyhow!(err).context(Error::ModuleRuntime))
                .fold(writer, |mut w, chunk| -> anyhow::Result<W> {
                    match chunk {
                        LogChunk::Stdin(b)
                        | LogChunk::Stdout(b)
                        | LogChunk::Stderr(b)
                        | LogChunk::Unknown(b) => w.write(&b).context(Error::Write)?,
                    };
                    Ok(w)
                })
        })
}
