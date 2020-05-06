// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::missing_errors_doc,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::similar_names,
    clippy::shadow_unrelated,
    clippy::too_many_lines,
    clippy::use_self
)]

use hsm_sys::{
    cert_properties_create, cert_properties_destroy, certificate_info_destroy,
    certificate_info_get_certificate, certificate_info_get_common_name,
    certificate_info_get_private_key, certificate_info_get_valid_to,
    certificate_info_private_key_type, hsm_client_crypto_deinit, hsm_client_crypto_init,
    hsm_client_crypto_interface, hsm_client_tpm_deinit, hsm_client_tpm_init,
    hsm_client_tpm_interface, hsm_client_x509_deinit, hsm_client_x509_init,
    hsm_client_x509_interface, hsm_get_device_ca_alias, hsm_get_version, set_alias,
    set_certificate_type, set_common_name, set_issuer_alias, set_san_entries, set_validity_seconds,
    CERTIFICATE_TYPE_CERTIFICATE_TYPE_CA, CERTIFICATE_TYPE_CERTIFICATE_TYPE_CLIENT,
    CERTIFICATE_TYPE_CERTIFICATE_TYPE_SERVER, CERTIFICATE_TYPE_CERTIFICATE_TYPE_UNKNOWN,
    CERT_INFO_HANDLE, CERT_PROPS_HANDLE, HSM_CLIENT_CRYPTO_INTERFACE, HSM_CLIENT_HANDLE,
    HSM_CLIENT_TPM_INTERFACE, HSM_CLIENT_X509_INTERFACE, PRIVATE_KEY_TYPE_PRIVATE_KEY_TYPE_PAYLOAD,
    PRIVATE_KEY_TYPE_PRIVATE_KEY_TYPE_REFERENCE, PRIVATE_KEY_TYPE_PRIVATE_KEY_TYPE_UNKNOWN,
    SIZED_BUFFER,
};

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
