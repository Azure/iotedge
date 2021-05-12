// Copyright (c) Microsoft. All rights reserved.

use std::io::Write;

use failure::Fail;
use futures::StreamExt;

use edgelet_core::{LogOptions, ModuleRuntime};

use crate::error::{Error, ErrorKind};

pub async fn pull_logs<W>(
    runtime: &impl ModuleRuntime,
    id: &str,
    options: &LogOptions,
    writer: &mut W,
) -> Result<(), Error>
where
    W: Write + Send,
{
    // Collect Logs
    let logs = runtime
        .logs(id, options)
        .await
        .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))?;

    // Write all logs
    let write = logs.map(|part| writer.write_all(part.as_ref()));

    // Extract errors
    write
        .collect::<Vec<_>>()
        .await
        .into_iter()
        .collect::<Result<(), std::io::Error>>()
        .map_err(|err| Error::from(err.context(ErrorKind::Write)))
}
