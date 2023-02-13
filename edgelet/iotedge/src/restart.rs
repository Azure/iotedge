// Copyright (c) Microsoft. All rights reserved.

use std::fmt::Write;
use std::sync::{Arc, Mutex};

use anyhow::Context;

use edgelet_core::ModuleRuntime;

use crate::error::Error;

pub struct Restart<M, W> {
    id: String,
    runtime: M,
    output: Arc<Mutex<W>>,
}

impl<M, W> Restart<M, W> {
    pub fn new(id: String, runtime: M, output: W) -> Self {
        Restart {
            id,
            runtime,
            output: Arc::new(Mutex::new(output)),
        }
    }
}

impl<M, W> Restart<M, W>
where
    M: ModuleRuntime,
    W: std::io::Write + Send,
{
    pub async fn execute(&self) -> anyhow::Result<()> {
        let mut output = String::new();

        // A stop request must be sent to workload socket manager first.
        // To properly restart, both the stop and start APIs must be called.
        if let Err(err) = self.runtime.stop(&self.id, None).await {
            writeln!(
                output,
                "warn: {} was not stopped gracefully: {}",
                self.id, err
            )?;
        }

        self.runtime.start(&self.id).await?;
        writeln!(output, "Restarted {}", self.id)?;

        let write = self.output.clone();
        let mut w = write.lock().unwrap();
        write!(w, "{}", output).context(Error::WriteToStdout)?;

        Ok(())
    }
}
