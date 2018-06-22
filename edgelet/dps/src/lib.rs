// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate base64;
extern crate bytes;
extern crate chrono;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
#[macro_use]
extern crate log;
#[macro_use]
extern crate percent_encoding;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate tokio;
#[cfg(test)]
extern crate tokio_core;
extern crate tokio_timer;
extern crate url;

extern crate edgelet_core;
extern crate edgelet_http;
extern crate edgelet_utils;

pub mod error;
mod model;
pub mod registration;

pub use error::{Error, ErrorKind};
pub use model::{
    DeviceRegistration, DeviceRegistrationResult, RegistrationOperationStatus,
    TpmRegistrationResult,
};
pub use registration::{DpsClient, DpsTokenSource};
