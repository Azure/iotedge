// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;

#[macro_use]
extern crate edgelet_utils;

mod client;
mod device;
mod error;
mod module;
mod model;

pub use client::Client;
pub use device::DeviceClient;
pub use module::ModuleClient;
pub use model::{AuthMechanism, AuthType, Module, Properties, Twin, X509Thumbprint};
