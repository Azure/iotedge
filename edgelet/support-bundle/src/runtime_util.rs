// Copyright (c) Microsoft. All rights reserved.

use std::io::Write;

use failure::Fail;
use futures::StreamExt;

use edgelet_core::{LogOptions, Module, ModuleRuntime};

use crate::error::{Error, ErrorKind};

pub async fn get_modules(
    runtime: &impl ModuleRuntime,
    include_ms_only: bool,
) -> Result<Vec<String>, Error> {
    const MS_MODULES: &[&str] = &["edgeAgent", "edgeHub"];

    runtime
        .list()
        .await
        .map(|modules| {
            modules
                .into_iter()
                .map(|module| module.name().to_owned())
                .filter(move |name| !include_ms_only || MS_MODULES.iter().any(|ms| ms == name))
                .collect()
        })
        .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
}

/// # Errors
///
/// Will return `Err` if docker is unable to fetch logs
pub async fn write_logs(
    runtime: &impl ModuleRuntime,
    module_name: &str,
    options: &LogOptions,
    writer: &mut (impl Write + Send),
) -> Result<(), Error> {
    // Collect Logs
    let logs = runtime
        .logs(module_name, options)
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
