// Copyright (c) Microsoft. All rights reserved.

extern crate bytes;
extern crate edgelet_core;
#[macro_use]
extern crate failure;
extern crate hsm;

mod certificate_properties;
mod crypto;
mod error;
pub mod tpm;

pub use crypto::{Certificate, Crypto};
pub use error::{Error, ErrorKind};
pub use tpm::{TpmKey, TpmKeyStore};
