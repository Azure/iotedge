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

pub const IOTEDGED_VALIDITY: u64 = 7_776_000; // 90 days
pub const IOTEDGED_COMMONNAME: &str = "iotedged workload ca";
pub const IOTEDGED_CA: &str = "iotedged-workload-ca";

pub use crypto::{Certificate, Crypto};
pub use error::{Error, ErrorKind};
pub use tpm::{TpmKey, TpmKeyStore};
