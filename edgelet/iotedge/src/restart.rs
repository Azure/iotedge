// Copyright (c) Microsoft. All rights reserved.

use std::io::Write;
use std::sync::{Arc, Mutex};

use failure::ResultExt;

use edgelet_core::ModuleRuntime;

use crate::error::{Error, ErrorKind};

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

impl<M, W, E> Restart<M, W>
where
    M: ModuleRuntime<Error = E>,
    Error: From<E>,
    W: Write + Send,
{
    pub async fn execute(&self) -> Result<(), Error> {
        let write = self.output.clone();
        self.runtime.restart(&self.id).await?;

        let mut w = write.lock().unwrap();
        writeln!(w, "{}", self.id).context(ErrorKind::WriteToStdout)?;
        Ok(())
    }
}
