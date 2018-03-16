// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate bytes;
extern crate chrono;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hmac;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate sha2;

mod crypto;
mod error;
mod module;

use std::rc::Rc;

use futures::future;
use futures::future::FutureResult;

pub use error::{Error, ErrorKind};
pub use module::{Module, ModuleRegistry, ModuleRuntime, ModuleRuntimeState, ModuleStatus};

pub struct Edgelet<T>
where
    T: ModuleRuntime,
{
    module_runtime: Rc<T>,
}

impl<T> Edgelet<T>
where
    T: ModuleRuntime,
{
    pub fn new(module_runtime: T) -> Edgelet<T> {
        Edgelet {
            module_runtime: Rc::new(module_runtime),
        }
    }

    pub fn start_edge_agent(&self) -> FutureResult<(), Error> {
        // TODO: Implement this in terms of operations on the module runtime
        future::ok(())
    }

    pub fn stop_edge_agent(&self) -> FutureResult<(), Error> {
        // TODO: Implement this in terms of operations on the module runtime
        future::ok(())
    }

    pub fn module_runtime(&self) -> Rc<T> {
        self.module_runtime.clone()
    }
}
