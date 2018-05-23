// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

#[cfg(test)]
extern crate base64;
extern crate bytes;
extern crate chrono;
extern crate consistenttime;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hmac;
#[macro_use]
extern crate log;
extern crate regex;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate sha2;

#[macro_use]
extern crate edgelet_utils;

mod certificate_properties;
pub mod crypto;
mod error;
mod identity;
mod module;
pub mod pid;
pub mod watchdog;

use std::rc::Rc;

use futures::{future, future::FutureResult};

pub use certificate_properties::{CertificateProperties, CertificateType};
pub use crypto::{Certificate, CreateCertificate, Decrypt, Encrypt, GetTrustBundle, KeyBytes,
                 KeyStore, PrivateKey, Signature};
pub use error::{Error, ErrorKind};
pub use identity::{AuthType, Identity, IdentityManager, IdentitySpec};
pub use module::{Module, ModuleRegistry, ModuleRuntime, ModuleRuntimeState, ModuleSpec,
                 ModuleStatus};

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
