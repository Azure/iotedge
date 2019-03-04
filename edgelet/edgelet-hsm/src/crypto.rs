// Copyright (c) Microsoft. All rights reserved.

use chrono::{DateTime, Utc};
use std::sync::{Arc, Mutex};

use failure::Fail;

use edgelet_core::{
    Certificate as CoreCertificate, CertificateProperties as CoreCertificateProperties,
    CreateCertificate as CoreCreateCertificate, Decrypt as CoreDecrypt, Encrypt as CoreEncrypt,
    Error as CoreError, ErrorKind as CoreErrorKind, GetTrustBundle as CoreGetTrustBundle,
    KeyBytes as CoreKeyBytes, MasterEncryptionKey as CoreMasterEncryptionKey,
    PrivateKey as CorePrivateKey,
};
pub use hsm::{
    Buffer, Decrypt, Encrypt, GetTrustBundle, HsmCertificate, KeyBytes as HsmKeyBytes,
    PrivateKey as HsmPrivateKey,
};
use hsm::{
    CreateCertificate as HsmCreateCertificate,
    CreateMasterEncryptionKey as HsmCreateMasterEncryptionKey, Crypto as HsmCrypto,
    DestroyMasterEncryptionKey as HsmDestroyMasterEncryptionKey,
};

use crate::certificate_properties::convert_properties;
pub use crate::error::{Error, ErrorKind};

/// The TPM Key Store.
/// Activate a private key, and then you can use that key to sign data.
#[derive(Clone)]
pub struct Crypto {
    crypto: Arc<Mutex<HsmCrypto>>,
}

impl Crypto {
    pub fn new() -> Result<Self, Error> {
        let hsm = HsmCrypto::new()?;
        Crypto::from_hsm(hsm)
    }

    pub fn from_hsm(crypto: HsmCrypto) -> Result<Self, Error> {
        Ok(Crypto {
            crypto: Arc::new(Mutex::new(crypto)),
        })
    }
}

impl CoreMasterEncryptionKey for Crypto {
    fn create_key(&self) -> Result<(), CoreError> {
        self.crypto
            .lock()
            .expect("Lock on crypto structure failed")
            .create_master_encryption_key()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))
    }

    fn destroy_key(&self) -> Result<(), CoreError> {
        self.crypto
            .lock()
            .expect("Lock on crypto structure failed")
            .destroy_master_encryption_key()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))
    }
}

impl CoreCreateCertificate for Crypto {
    type Certificate = Certificate;

    fn create_certificate(
        &self,
        properties: &CoreCertificateProperties,
    ) -> Result<Self::Certificate, CoreError> {
        let crypto = self.crypto.lock().expect("Lock on crypto structure failed");
        let device_ca_alias = crypto.get_device_ca_alias();
        let cert = crypto
            .create_certificate(&convert_properties(properties, &device_ca_alias))
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))?;
        Ok(Certificate(cert))
    }

    fn destroy_certificate(&self, alias: String) -> Result<(), CoreError> {
        self.crypto
            .lock()
            .expect("Lock on crypto structure failed")
            .destroy_certificate(alias)
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))?;
        Ok(())
    }
}

impl CoreEncrypt for Crypto {
    type Buffer = Buffer;

    fn encrypt(
        &self,
        client_id: &[u8],
        plaintext: &[u8],
        initialization_vector: &[u8],
    ) -> Result<Self::Buffer, CoreError> {
        self.crypto
            .lock()
            .expect("Lock on crypto structure failed")
            .encrypt(client_id, plaintext, initialization_vector)
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))
    }
}

impl CoreDecrypt for Crypto {
    type Buffer = Buffer;

    fn decrypt(
        &self,
        client_id: &[u8],
        ciphertext: &[u8],
        initialization_vector: &[u8],
    ) -> Result<Self::Buffer, CoreError> {
        self.crypto
            .lock()
            .expect("Lock on crypto structure failed")
            .decrypt(client_id, ciphertext, initialization_vector)
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))
    }
}

impl CoreGetTrustBundle for Crypto {
    type Certificate = Certificate;

    fn get_trust_bundle(&self) -> Result<Self::Certificate, CoreError> {
        let cert = self
            .crypto
            .lock()
            .expect("Lock on crypto structure failed")
            .get_trust_bundle()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))?;
        Ok(Certificate(cert))
    }
}

#[derive(Debug)]
pub struct Certificate(HsmCertificate);

impl CoreCertificate for Certificate {
    type Buffer = String;
    type KeyBuffer = Vec<u8>;

    fn pem(&self) -> Result<Self::Buffer, CoreError> {
        self.0
            .pem()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))
    }

    fn get_private_key(&self) -> Result<Option<CorePrivateKey<Self::KeyBuffer>>, CoreError> {
        self.0
            .get_private_key()
            .map(|pk| match pk {
                Some(HsmPrivateKey::Key(HsmKeyBytes::Pem(key_buffer))) => {
                    Some(CorePrivateKey::Key(CoreKeyBytes::Pem(key_buffer)))
                }
                Some(HsmPrivateKey::Ref(key_string)) => Some(CorePrivateKey::Ref(key_string)),
                None => None,
            })
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))
    }

    fn get_valid_to(&self) -> Result<DateTime<Utc>, CoreError> {
        self.0
            .get_valid_to()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))
    }
}
