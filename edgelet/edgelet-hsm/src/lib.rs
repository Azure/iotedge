// Copyright (c) Microsoft. All rights reserved.

extern crate edgelet_core;
#[macro_use]
extern crate failure;
extern crate hsm;

mod certificate_properties;
mod crypto;
mod error;
mod tpm;

pub use error::{Error, ErrorKind};
pub use tpm::{TpmKey, TpmKeyStore};
pub use crypto::{Certificate, Crypto};
