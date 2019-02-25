// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]

#[macro_use]
extern crate log;
#[macro_use]
extern crate percent_encoding;
#[macro_use]
extern crate serde_derive;

pub mod dps;
pub mod error;
mod model;
pub mod registration;

pub use error::{Error, ErrorKind};
pub use model::{
    DeviceRegistration, DeviceRegistrationResult, RegistrationOperationStatus,
    TpmRegistrationResult,
};
pub use registration::{DpsClient, DpsTokenSource};

pub const DPS_API_VERSION: &str = "2018-11-01";
