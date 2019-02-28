// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]

extern crate bytes;
extern crate chrono;
extern crate edgelet_core;
extern crate failure;
extern crate hsm;

mod certificate_properties;
mod crypto;
mod error;
pub mod tpm;

pub use crypto::{Certificate, Crypto};
pub use error::{Error, ErrorKind};
pub use tpm::{TpmKey, TpmKeyStore};
