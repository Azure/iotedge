// Copyright (c) Microsoft. All rights reserved.

use std::io::Write;
use std::sync::{Arc, Mutex};

use failure::{Fail, ResultExt};

use docker::DockerApi;

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

impl<M, W> Restart<M, W>
where
    M: 'static + DockerApi + Clone,
    W: 'static + Write + Send,
{
    pub async fn execute(&self) -> Result<(), Error> {
        let write = self.output.clone();
        self.runtime
            .container_restart(&self.id, None)
            .await
            .map_err(|err| Error::from(ErrorKind::Docker(err.to_string())))?;

        let mut w = write.lock().unwrap();
        writeln!(w, "{}", self.id).context(ErrorKind::WriteToStdout)?;
        Ok(())
    }
}
