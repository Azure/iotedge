use std::cell::RefCell;
use std::io::Write;
use std::sync::Arc;

use edgelet_core::{Module, ModuleRuntime};
use futures::{future, Future};

use Command;
use error::{Error, ErrorKind};

pub struct List<M, W> {
    runtime: M,
    output: Arc<RefCell<W>>,
}

impl<M, W> List<M, W> {
    pub fn new(runtime: M, output: W) -> Self {
        List {
            runtime,
            output: Arc::new(RefCell::new(output)),
        }
    }
}

impl<M, W> Command for List<M, W>
where
    M: 'static + ModuleRuntime + Clone,
    M::Module: Clone,
    W: 'static + Write,
{
    type Future = Box<Future<Item = (), Error = Error>>;

    fn execute(&mut self) -> Self::Future {
        let write = self.output.clone();
        let result = self.runtime
            .list()
            .map_err(|_| Error::from(ErrorKind::ModuleRuntime))
            .and_then(move |list| {
                let modules = list.clone();
                let futures = list.into_iter().map(|m| m.runtime_state());
                future::join_all(futures)
                    .map_err(|_e| Error::from(ErrorKind::ModuleRuntime))
                    .and_then(move |states| {
                        let mut w = write.borrow_mut();
                        writeln!(w, "{0: <20} {1: <20} {2: <20}", "NAME", "TYPE", "STATUS")?;
                        for (module, state) in modules.iter().zip(states) {
                            writeln!(
                                w,
                                "{0: <20} {1: <20} {2: <20}",
                                module.name(),
                                module.type_(),
                                state.status()
                            )?;
                        }
                        Ok(())
                    })
            });
        Box::new(result)
    }
}
