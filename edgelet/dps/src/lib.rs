// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::stutter, clippy::use_self)]

extern crate base64;
extern crate bytes;
extern crate chrono;
extern crate failure;
extern crate futures;
#[cfg(test)]
extern crate http;
extern crate hyper;
#[macro_use]
extern crate log;
#[macro_use]
extern crate percent_encoding;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate tokio;
extern crate url;

extern crate edgelet_core;
extern crate edgelet_http;

pub mod error;
mod model;
pub mod registration;

pub use error::{Error, ErrorKind};
pub use model::{
    DeviceRegistration, DeviceRegistrationResult, RegistrationOperationStatus,
    TpmRegistrationResult,
};
pub use registration::{DpsClient, DpsTokenSource};
