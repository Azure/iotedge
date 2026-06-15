// Copyright (c) Microsoft. All rights reserved.

use std::io::{self, Write};

use anyhow::Context;
use http_body::Body as _;

use edgelet_core::{LogOptions, Module, ModuleRuntime};

use crate::error::Error;

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
) -> anyhow::Result<()> {
    // Collect Logs
    let mut logs = runtime
        .logs(module_name, options)
        .await
        .context(Error::Write)?;

    while let Some(frame) =
        futures_util::future::poll_fn(|cx| std::pin::Pin::new(&mut logs).poll_frame(cx)).await
    {
        let bytes = frame
            .context(Error::Write)?
            .into_data()
            .map_err(|_| io::Error::from(io::ErrorKind::UnexpectedEof))
            .context(Error::Write)?;
        // First 4 bytes represent stderr vs stdout, we currently don't display differently based on that.
        // Next 4 bytes represent length of chunk, rust already encodes this information in the slice.
        if bytes.len() > 8 {
            writer.write_all(&bytes[8..]).context(Error::Write)?;
        }
    }

    Ok(())
}
