// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_lines,
    clippy::use_self
)]

use std::sync::{Arc, Mutex};

mod certificate_properties;
mod crypto;
mod error;
pub mod tpm;
pub mod x509;

pub use crypto::{Certificate, Crypto};
pub use error::{Error, ErrorKind};
pub use tpm::{TpmKey, TpmKeyStore};
pub use x509::X509;

#[derive(Debug)]
pub struct HsmLock(Mutex<()>);

impl HsmLock {
    /// Use this instance of `Arc<HsmLock>` for all operations related to the HSM.
    /// This ensures that access to any HSM operation is serialized by this lock.
    pub fn new() -> Arc<Self> {
        Arc::new(HsmLock(Mutex::new(())))
    }
}
