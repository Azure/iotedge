// Copyright (c) Microsoft. All rights reserved.

use std::io::Write;

use failure::Fail;
use futures::StreamExt;

use edgelet_core::{LogOptions, Module, ModuleRuntime};

use crate::error::{Error, ErrorKind};

pub async fn get_modules(runtime: &impl ModuleRuntime, include_ms_only: bool) -> Vec<String> {
    const MS_MODULES: &[&str] = &["edgeAgent", "edgeHub"];

    // Getting modules requires the management socket, which might not be available if
    // aziot-edged hasn't started. Require this operation to complete within a timeout
    // so it doesn't block forever on an unavailable socket.
    let list = tokio::time::timeout(std::time::Duration::from_secs(30), runtime.list());

    if let Ok(Ok(modules)) = list.await {
        modules
            .into_iter()
            .map(|module| module.name().to_owned())
            .filter(move |name| !include_ms_only || MS_MODULES.iter().any(|ms| ms == name))
            .collect()
    } else {
        println!("Warning: Unable to call management socket. Module list not available.");

        Vec::new()
    }
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
    let logs = runtime.logs(module_name, options).await;

    // Write all logs
    let write = logs.map(|part| {
        let part = part.map_err(|err| {
            println!("Warning: error gathering logs: {}", err);

            std::io::Error::new(std::io::ErrorKind::Other, err.to_string())
        })?;

        writer.write_all(part.as_ref())
    });

    // Extract errors
    write
        .collect::<Vec<_>>()
        .await
        .into_iter()
        .collect::<Result<(), std::io::Error>>()
        .map_err(|err| Error::from(err.context(ErrorKind::Write)))
}
