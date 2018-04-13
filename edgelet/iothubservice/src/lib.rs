// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
#[cfg(test)]
extern crate tokio_core;
extern crate url;

#[macro_use]
extern crate edgelet_utils;

mod client;
mod device;
pub mod error;
mod model;

pub use client::Client;
pub use device::DeviceClient;
pub use model::{AuthMechanism, AuthType, Module, Properties, SymmetricKey, Twin, X509Thumbprint};
