// Copyright (c) Microsoft. All rights reserved.

use chrono::{DateTime, Utc};
use std::sync::Arc;

use failure::Fail;

use edgelet_core::{
    Certificate as CoreCertificate, CertificateIssuer as CoreCertificateIssuer,
    CertificateProperties as CoreCertificateProperties, CreateCertificate as CoreCreateCertificate,
    Decrypt as CoreDecrypt, Encrypt as CoreEncrypt, Error as CoreError, ErrorKind as CoreErrorKind,
    GetHsmVersion as CoreGetHsmVersion, GetIssuerAlias as CoreGetIssuerAlias,
    GetTrustBundle as CoreGetTrustBundle, KeyBytes as CoreKeyBytes, MakeRandom as CoreMakeRandom,
    MasterEncryptionKey as CoreMasterEncryptionKey, PrivateKey as CorePrivateKey,
};
pub use hsm::{
    Buffer, Decrypt, Encrypt, GetCertificate as HsmGetCertificate, GetTrustBundle, HsmCertificate,
    KeyBytes as HsmKeyBytes, PrivateKey as HsmPrivateKey,
};
use hsm::{
    CreateCertificate as HsmCreateCertificate,
    CreateMasterEncryptionKey as HsmCreateMasterEncryptionKey, Crypto as HsmCrypto,
    DestroyMasterEncryptionKey as HsmDestroyMasterEncryptionKey, MakeRandom as HsmMakeRandom,
};

use crate::certificate_properties::convert_properties;
pub use crate::error::{Error, ErrorKind};
use crate::HsmLock;

/// The TPM Key Store.
/// Activate a private key, and then you can use that key to sign data.
#[derive(Clone)]
pub struct Crypto {
    crypto: Arc<HsmCrypto>,
    hsm_lock: Arc<HsmLock>,
}

// HsmCrypto is Send and !Sync. However Crypto can be Sync since all access to Crypto::crypto
// is controlled by the methods of Crypto, which all lock Crypto::hsm_lock first.
//
// For the same reason, Crypto also needs an explicit Send impl
// since Arc<T>: Send requires T: Send + Sync.
unsafe impl Send for Crypto {}
unsafe impl Sync for Crypto {}

impl Crypto {
    pub fn new(
        hsm_lock: Arc<HsmLock>,
        auto_generated_ca_lifetime_seconds: u64,
    ) -> Result<Self, Error> {
        let hsm = HsmCrypto::new(auto_generated_ca_lifetime_seconds)?;
        Crypto::from_hsm(hsm, hsm_lock)
    }

    pub fn from_hsm(crypto: HsmCrypto, hsm_lock: Arc<HsmLock>) -> Result<Self, Error> {
        Ok(Crypto {
            crypto: Arc::new(crypto),
            hsm_lock,
        })
    }
}

impl CoreGetHsmVersion for Crypto {
    fn get_version(&self) -> Result<String, CoreError> {
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        self.crypto
            .get_version()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::HsmVersion)))
    }
}

impl CoreMasterEncryptionKey for Crypto {
    fn create_key(&self) -> Result<(), CoreError> {
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        self.crypto
            .create_master_encryption_key()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))
    }

    fn destroy_key(&self) -> Result<(), CoreError> {
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        self.crypto
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
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        let device_ca_alias = self.crypto.get_device_ca_alias();
        let cert = self
            .crypto
            .create_certificate(&convert_properties(properties, &device_ca_alias))
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::CertificateCreate)))?;
        Ok(Certificate(cert))
    }

    fn destroy_certificate(&self, alias: String) -> Result<(), CoreError> {
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        self.crypto
            .destroy_certificate(alias)
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::CertificateDestroy)))?;
        Ok(())
    }

    fn get_certificate(&self, alias: String) -> Result<Self::Certificate, CoreError> {
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        let cert = self
            .crypto
            .get(alias)
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::CertificateGet)))?;
        Ok(Certificate(cert))
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
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        self.crypto
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
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        self.crypto
            .decrypt(client_id, ciphertext, initialization_vector)
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))
    }
}

impl CoreGetIssuerAlias for Crypto {
    fn get_issuer_alias(&self, issuer: CoreCertificateIssuer) -> Result<String, CoreError> {
        if issuer == CoreCertificateIssuer::DeviceCa {
            let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
            Ok(self.crypto.get_device_ca_alias())
        } else {
            Err(CoreError::from(CoreErrorKind::InvalidIssuer))
        }
    }
}

impl CoreGetTrustBundle for Crypto {
    type Certificate = Certificate;

    fn get_trust_bundle(&self) -> Result<Self::Certificate, CoreError> {
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        let cert = self
            .crypto
            .get_trust_bundle()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::CertificateGet)))?;
        Ok(Certificate(cert))
    }
}

impl CoreMakeRandom for Crypto {
    fn get_random_bytes(&self, buffer: &mut [u8]) -> Result<(), CoreError> {
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        self.crypto
            .get_random_bytes(buffer)
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::MakeRandom)))?;
        Ok(())
    }
}

#[derive(Debug)]
pub struct Certificate(HsmCertificate);

impl Certificate {
    pub fn new(cert: HsmCertificate) -> Certificate {
        Certificate(cert)
    }
}

impl CoreCertificate for Certificate {
    type Buffer = String;
    type KeyBuffer = Vec<u8>;

    fn pem(&self) -> Result<Self::Buffer, CoreError> {
        self.0
            .pem()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::CertificateContent)))
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
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::CertificateKey)))
    }

    fn get_valid_to(&self) -> Result<DateTime<Utc>, CoreError> {
        self.0
            .get_valid_to()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::CertificateDetail)))
    }

    fn get_common_name(&self) -> Result<String, CoreError> {
        self.0
            .get_common_name()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::CertificateDetail)))
    }
}
