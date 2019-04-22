// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]

mod certificate_properties;
mod crypto;
mod error;
pub mod tpm;
pub mod x509;

pub use crypto::{Certificate, Crypto};
pub use error::{Error, ErrorKind};
pub use tpm::{TpmKey, TpmKeyStore};
pub use x509::X509;
