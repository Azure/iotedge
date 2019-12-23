// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::similar_names,
    clippy::shadow_unrelated,
    clippy::too_many_lines,
    clippy::use_self
)]

use hsm_sys::*;

mod crypto;
mod error;
pub mod tpm;
mod x509;

pub use crate::crypto::{
    Buffer, CertificateProperties, CertificateType, Crypto, HsmCertificate, KeyBytes, PrivateKey,
};
pub use crate::error::{Error, ErrorKind};
pub use crate::tpm::{Tpm, TpmDigest, TpmKey};
pub use crate::x509::{PrivateKeySignDigest, X509Data, X509};

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

pub trait GetDeviceIdentityCertificate {
    fn get_cert(&self) -> Result<X509Data, Error>;
    fn get_key(&self) -> Result<X509Data, Error>;
    fn get_common_name(&self) -> Result<String, Error>;
    fn sign_with_private_key(&self, data: &[u8]) -> Result<PrivateKeySignDigest, Error>;
    fn get_certificate_info(&self) -> Result<HsmCertificate, Error>;
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

pub trait GetCertificate {
    fn get(&self, alias: String) -> Result<HsmCertificate, Error>;
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
