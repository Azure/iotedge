// Copyright (c) Microsoft. All rights reserved.

use std::cell::RefCell;
use std::io::Write;
use std::sync::Arc;

use edgelet_core::ModuleRuntime;
use futures::Future;

use Command;
use error::{Error, ErrorKind};

pub struct Stop<M, W> {
    id: String,
    runtime: M,
    output: Arc<RefCell<W>>,
}

impl<M, W> Stop<M, W> {
    pub fn new(id: String, runtime: M, output: W) -> Self {
        Stop {
            id,
            runtime,
            output: Arc::new(RefCell::new(output)),
        }
    }
}

impl<M, W> Command for Stop<M, W>
where
    M: 'static + ModuleRuntime + Clone,
    W: 'static + Write,
{
    type Future = Box<Future<Item = (), Error = Error>>;

    fn execute(&mut self) -> Self::Future {
        let id = self.id.clone();
        let write = self.output.clone();
        let result = self.runtime
            .stop(&id)
            .map_err(|_| Error::from(ErrorKind::ModuleRuntime))
            .and_then(move |_| {
                let mut w = write.borrow_mut();
                writeln!(w, "{}", id)?;
                Ok(())
            });
        Box::new(result)
    }
}
