// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_lines,
    clippy::use_self
)]

mod device;
pub mod error;
mod model;

pub use crate::device::DeviceClient;
pub use crate::error::{Error, ErrorKind, ModuleOperationReason};
pub use crate::model::{
    AuthMechanism, AuthType, Module, Properties, SymmetricKey, Twin, X509Thumbprint,
};
