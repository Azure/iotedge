// Copyright (c) Microsoft. All rights reserved.

use std::io::Write;
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
    W: Write + Send,
{
    pub async fn execute(&self) -> anyhow::Result<()> {
        let write = self.output.clone();
        self.runtime.restart(&self.id).await?;

        let mut w = write.lock().unwrap();
        writeln!(w, "{}", self.id).context(Error::WriteToStdout)?;
        Ok(())
    }
}
