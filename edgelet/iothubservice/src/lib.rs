// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate chrono;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate log;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
#[cfg(test)]
extern crate tokio_core;
extern crate url;

extern crate edgelet_core;
extern crate edgelet_http;
#[macro_use]
extern crate edgelet_utils;

mod device;
pub mod error;
mod model;

pub use device::DeviceClient;
pub use error::{Error, ErrorKind};
pub use model::{AuthMechanism, AuthType, Module, Properties, SymmetricKey, Twin, X509Thumbprint};
