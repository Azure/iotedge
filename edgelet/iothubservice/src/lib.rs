// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate futures;
extern crate hyper;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;

mod client;
mod device;
mod module;
mod twin;

pub use client::Client;
pub use device::DeviceClient;
pub use module::ModuleClient;
pub use twin::{AuthType, Properties, Twin};
