// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cyclomatic_complexity,
    clippy::module_name_repetitions,
    clippy::similar_names,
    clippy::shadow_unrelated,
    clippy::use_self
)]

extern crate chrono;
extern crate failure;
extern crate hsm_sys;

use hsm_sys::*;

mod crypto;
mod error;
pub mod tpm;
mod x509;

pub use crypto::{
    Buffer, CertificateProperties, CertificateType, Crypto, HsmCertificate, KeyBytes, PrivateKey,
};
pub use error::{Error, ErrorKind};
pub use tpm::{Tpm, TpmDigest, TpmKey};
pub use x509::{X509Data, X509};

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
    fn get_random_bytes(&self, buffer: &mut [u8]) -> Result<(), Error>;
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

    fn destroy_certificate(&self, alias: String) -> Result<(), Error>;
}

pub trait Encrypt {
    fn encrypt(
        &self,
        client_id: &[u8],
        plaintext: &[u8],
        initialization_vector: &[u8],
    ) -> Result<Buffer, Error>;
}

pub trait Decrypt {
    fn decrypt(
        &self,
        client_id: &[u8],
        ciphertext: &[u8],
        initialization_vector: &[u8],
    ) -> Result<Buffer, Error>;
}

pub trait GetTrustBundle {
    fn get_trust_bundle(&self) -> Result<HsmCertificate, Error>;
}
