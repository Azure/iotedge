// Copyright (c) Microsoft. All rights reserved.
#[macro_use]
extern crate failure;
extern crate hsm_sys;

use std::ops::Drop;
use hsm_sys::*;

mod error;
mod tpm;
mod x509;

pub use error::{Error, ErrorKind};
pub use tpm::{HsmTpm, TpmKey};
pub use x509::{HsmX509, X509Data};

// General HSM functions.

pub struct HsmSystem {}

impl HsmSystem {
    /// Called once in the beginning to initialize the HSM system
    pub fn new() -> Result<HsmSystem, Error> {
        let result = unsafe { initialize_hsm_system() as isize };
        if result != 0 {
            return Err(Error::from(result));
        }
        let result = unsafe { hsm_client_x509_init() as isize };
        if result != 0 {
            unsafe { deinitialize_hsm_system() };
            return Err(Error::from(result));
        }
        let result = unsafe { hsm_client_tpm_init() as isize };
        if result != 0 {
            unsafe {
                deinitialize_hsm_system();
                hsm_client_x509_deinit();
            };
            return Err(Error::from(result));
        }
        Ok(HsmSystem {})
    }
}

/// Called once at the end to deinitialize the HSM system
impl Drop for HsmSystem {
    fn drop(&mut self) {
        unsafe {
            deinitialize_hsm_system();
            hsm_client_x509_deinit();
            hsm_client_tpm_deinit();
        };
    }
}

// Traits

trait ManageTpmKeys {
    fn activate_identity_key(&self, key: &[u8]) -> Result<(), Error>;
    fn get_ek(&self) -> Result<TpmKey, Error>;
    fn get_srk(&self) -> Result<TpmKey, Error>;
}

trait SignWithTpm {
    fn sign_with_identity(&self, data: &[u8]) -> Result<TpmKey, Error>;
}

trait GetCerts {
    fn get_cert(&self) -> Result<X509Data, Error>;
    fn get_key(&self) -> Result<X509Data, Error>;
    fn get_common_name(&self) -> Result<String, Error>;
}
