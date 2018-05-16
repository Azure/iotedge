// Copyright (c) Microsoft. All rights reserved.

use std::sync::{Arc, RwLock};

use certificate_properties::convert_properties;
use edgelet_core::{Certificate as CoreCertificate,
                   CertificateProperties as CoreCertificateProperties,
                   CreateCertificate as CoreCreateCertificate, Decrypt as CoreDecrypt,
                   Encrypt as CoreEncrypt, Error as CoreError,
                   GetTrustBundle as CoreGetTrustBundle, KeyBytes as CoreKeyBytes,
                   PrivateKey as CorePrivateKey};

use super::{IOTEDGED_CA, IOTEDGED_COMMONNAME, IOTEDGED_VALIDITY};
pub use error::{Error, ErrorKind};
pub use hsm::{Buffer, Decrypt, Encrypt, GetTrustBundle, HsmCertificate, KeyBytes as HsmKeyBytes,
              PrivateKey as HsmPrivateKey};
use hsm::{CertificateProperties as HsmCertificateProperties,
          CertificateType as HsmCertificateType, CreateCertificate as HsmCreateCertificate,
          Crypto as HsmCrypto};

/// The TPM Key Store.
/// Activate a private key, and then you can use that key to sign data.
#[derive(Clone, Default)]
pub struct Crypto {
    crypto: Arc<RwLock<HsmCrypto>>,
}

impl Crypto {
    pub fn new(crypto: HsmCrypto) -> Result<Crypto, Error> {
        let edgelet_ca_props = HsmCertificateProperties::new(
            IOTEDGED_VALIDITY,
            IOTEDGED_COMMONNAME.to_string(),
            HsmCertificateType::Ca,
            crypto.get_device_ca_alias(),
            IOTEDGED_CA.to_string(),
        );
        let _root_cert = crypto
            .create_certificate(&edgelet_ca_props)
            .map_err(Error::from)?;
        Ok(Crypto {
            crypto: Arc::new(RwLock::new(crypto)),
        })
    }
}

impl CoreCreateCertificate for Crypto {
    type Certificate = Certificate;

    fn create_certificate(
        &self,
        properties: &CoreCertificateProperties,
    ) -> Result<Self::Certificate, CoreError> {
        let cert = self.crypto
            .read()
            .expect("Shared read lock on crypto structure failed")
            .create_certificate(&convert_properties(properties))
            .map_err(Error::from)
            .map_err(CoreError::from)?;
        Ok(Certificate(cert))
    }
}

impl CoreEncrypt for Crypto {
    type Buffer = Buffer;

    fn encrypt(
        &self,
        client_id: &[u8],
        plaintext: &[u8],
        passphrase: Option<&[u8]>,
        initialization_vector: &[u8],
    ) -> Result<Self::Buffer, CoreError> {
        self.crypto
            .read()
            .expect("Shared read lock on crypto structure failed")
            .encrypt(client_id, plaintext, passphrase, initialization_vector)
            .map_err(Error::from)
            .map_err(CoreError::from)
    }
}

impl CoreDecrypt for Crypto {
    type Buffer = Buffer;

    fn decrypt(
        &self,
        client_id: &[u8],
        ciphertext: &[u8],
        passphrase: Option<&[u8]>,
        initialization_vector: &[u8],
    ) -> Result<Self::Buffer, CoreError> {
        self.crypto
            .read()
            .expect("Shared read lock on crypto structure failed")
            .decrypt(client_id, ciphertext, passphrase, initialization_vector)
            .map_err(Error::from)
            .map_err(CoreError::from)
    }
}

impl CoreGetTrustBundle for Crypto {
    type Certificate = Certificate;

    fn get_trust_bundle(&self) -> Result<Self::Certificate, CoreError> {
        let cert = self.crypto
            .read()
            .expect("Shared lock on crypto structure failed")
            .get_trust_bundle()
            .map_err(Error::from)
            .map_err(CoreError::from)?;
        Ok(Certificate(cert))
    }
}

pub struct Certificate(HsmCertificate);

impl CoreCertificate for Certificate {
    type Buffer = String;
    type KeyBuffer = Vec<u8>;

    fn pem(&self) -> Result<Self::Buffer, CoreError> {
        self.0.pem().map_err(Error::from).map_err(CoreError::from)
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
            .map_err(Error::from)
            .map_err(CoreError::from)
    }
}
