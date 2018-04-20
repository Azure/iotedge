// Copyright (c) Microsoft. All rights reserved.
#[macro_use]
extern crate failure;
extern crate hsm_sys;

use std::ops::Drop;
use std::sync::{Once, ONCE_INIT};
use hsm_sys::*;

mod error;
mod tpm;
mod x509;
mod crypto;

pub use error::{Error, ErrorKind};
pub use tpm::{Tpm, TpmDigest, TpmKey};
pub use x509::{X509, X509Data};
pub use crypto::{CertificateProperties, Crypto, DecryptedBuffer, EncryptedBuffer, HsmCertificate};

// General HSM functions.
// TODO: Rust doesn't guarantee dropping static variables, this code
// may need to be scrapped in favor of creating a local variable in main().
static mut HSM_SYSTEM: Option<HsmSystem> = None;
static HSM_INIT: Once = ONCE_INIT;

fn get_hsm() -> &'static Option<HsmSystem> {
    unsafe {
        HSM_INIT.call_once(|| {
            HSM_SYSTEM = Some(HsmSystem::new().expect("HSM system failed to initialize"));
        });
        &HSM_SYSTEM
    }
}

struct HsmSystem {}

impl HsmSystem {
    /// Called once in the beginning to initialize the HSM system
    /// /// TODO:: add hsm_client_crypto_init()
    fn new() -> Result<HsmSystem, Error> {
        let result = unsafe { hsm_client_x509_init() as isize };
        if result != 0 {
            Err(result)?
        }
        let result = unsafe { hsm_client_tpm_init() as isize };
        if result != 0 {
            unsafe {
                hsm_client_x509_deinit();
            };
            Err(result)?
        }
        Ok(HsmSystem {})
    }
}

/// Called once at the end to deinitialize the HSM system
impl Drop for HsmSystem {
    fn drop(&mut self) {
        unsafe {
            hsm_client_x509_deinit();
            hsm_client_tpm_deinit();
        };
    }
}

// Traits

pub trait ManageTpmKeys {
    fn activate_identity_key(&self, key: &[u8]) -> Result<(), Error>;
    fn get_ek(&self) -> Result<TpmKey, Error>;
    fn get_srk(&self) -> Result<TpmKey, Error>;
}

pub trait SignWithTpm {
    fn sign_with_identity(&self, data: &[u8]) -> Result<TpmDigest, Error>;
    fn derive_and_sign_with_identity(
        &self,
        data: &[u8],
        identity: &[u8],
    ) -> Result<TpmDigest, Error>;
}

pub trait GetCerts {
    fn get_cert(&self) -> Result<X509Data, Error>;
    fn get_key(&self) -> Result<X509Data, Error>;
    fn get_common_name(&self) -> Result<String, Error>;
}

pub trait MakeRandom {
    fn get_random_number_limits(&self) -> Result<(isize, isize), Error>;
    fn get_random_number(&self) -> Result<usize, Error>;
}

pub trait CreateMasterEncryptionKey {
    fn create_master_encryption_key(&self) -> Result<(), Error>;
}

pub trait DestroyMasterEncryptionKey {
    fn destroy_master_encryption_key(&self) -> Result<(), Error>;
}

pub trait CreateCertificate {
    fn create_certificate(
        &self,
        properties: &CertificateProperties,
    ) -> Result<HsmCertificate, Error>;
}

pub trait EncryptData {
    fn encrypt(
        &self,
        client_id: &[u8],
        plaintext: &[u8],
        passphrase: Option<&[u8]>,
        initialization_vector: &[u8],
    ) -> Result<EncryptedBuffer, Error>;
}

pub trait DecryptData {
    fn decrypt(
        &self,
        client_id: &[u8],
        ciphertext: &[u8],
        passphrase: Option<&[u8]>,
        initialization_vector: &[u8],
    ) -> Result<DecryptedBuffer, Error>;
}
