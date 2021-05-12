// Copyright (c) Microsoft. All rights reserved.

use std::io::Write;

use failure::Fail;
use futures::StreamExt;

use edgelet_core::{LogOptions, Module, ModuleRuntime};

use crate::error::{Error, ErrorKind};

struct LogManager<M>
where
    M: ModuleRuntime,
{
    runtime: M,
    include_ms_only: bool,
}

impl<M> LogManager<M>
where
    M: ModuleRuntime,
{
    async fn write_module_logs(
        &self,
        options: &LogOptions,
        writer: &mut (impl Write + Send),
    ) -> Result<(), Error> {
        for module in self.get_modules().await? {
            self.write_logs(&module, options, writer).await?;
        }

        Ok(())
    }

    async fn get_modules(&self) -> Result<Vec<String>, Error> {
        const MS_MODULES: &[&str] = &["edgeAgent", "edgeHub"];

        let include_ms_only = self.include_ms_only;

        let runtime_modules = self
            .runtime
            .list_with_details()
            .await
            .into_iter()
            .map(|(module, _s)| module.name().to_owned())
            .filter(move |name| !include_ms_only || MS_MODULES.iter().any(|ms| ms == name))
            .collect();

        Ok(runtime_modules)
    }
    async fn write_logs(
        &self,
        module_id: &str,
        options: &LogOptions,
        writer: &mut (impl Write + Send),
    ) -> Result<(), Error> {
        // Collect Logs
        let logs = self
            .runtime
            .logs(module_id, options)
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
}
