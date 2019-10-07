// Copyright (c) Microsoft. All rights reserved.

use std::io::Write;
use std::sync::{Arc, Mutex};

use failure::{Fail, ResultExt};
use futures::Future;

use edgelet_core::ModuleRuntime;

use crate::error::{Error, ErrorKind};
use crate::Command;

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

impl<M, W> Command for Restart<M, W>
where
    M: 'static + ModuleRuntime + Clone,
    W: 'static + Write + Send,
{
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(self) -> Self::Future {
        let id = self.id.clone();
        let write = self.output.clone();
        let result = self
            .runtime
            .restart(&id)
            .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            .and_then(move |_| {
                let mut w = write.lock().unwrap();
                writeln!(w, "{}", id).context(ErrorKind::WriteToStdout)?;
                Ok(())
            });
        Box::new(result)
    }
}
