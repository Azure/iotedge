#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use std::sync::{Arc, RwLock};

#[cfg(unix)]
use openssl::pkcs12::Pkcs12;
#[cfg(unix)]
use openssl::pkey::PKey;
#[cfg(unix)]
use openssl::stack::Stack;
#[cfg(unix)]
use openssl::x509::X509;

use edgelet_core::crypto::{
    Certificate as CryptoCertificate, CreateCertificate, KeyBytes, PrivateKey, Signature,
};
use edgelet_core::CertificateProperties;
use failure::ResultExt;

pub use crate::error::{Error, ErrorKind};

#[derive(Clone)]
pub struct CertificateManager<C: CreateCertificate + Clone> {
    certificate: Arc<RwLock<Option<Certificate>>>,
    crypto: C,
    props: CertificateProperties,
}

#[derive(Clone)]
struct Certificate {
    cert: String,
    private_key: String,
}

impl<C: CreateCertificate + Clone> CertificateManager<C> {
    pub fn new(crypto: C, props: CertificateProperties) -> Self {
        Self {
            certificate: Arc::new(RwLock::new(None)),
            crypto,
            props,
        }
    }

    // Convenience function since native-tls does not yet support PEM
    // and since everything else uses PEM certificates, we want to keep
    // the actual storage of the certificate in the PEM format.
    #[cfg(unix)]
    pub fn get_pkcs12_certificate(&self) -> Result<Vec<u8>, Error> {
        let stored_cert_bundle = self.get_certificate()?;

        let cert = stored_cert_bundle.cert.as_bytes();

        let mut certs =
            X509::stack_from_pem(cert).with_context(|_| ErrorKind::CertificateConversionError)?;

        let mut ca_certs = Stack::new().with_context(|_| ErrorKind::CertificateConversionError)?;
        for cert in certs.split_off(1) {
            ca_certs
                .push(cert)
                .with_context(|_| ErrorKind::CertificateConversionError)?;
        }

        let key = PKey::private_key_from_pem(stored_cert_bundle.private_key.as_bytes())
            .expect("Error processing private key from pem");

        let server_cert = &certs[0];
        let mut builder = Pkcs12::builder();
        builder.ca(ca_certs);
        let pkcs_certs = builder
            .build("", "", &key, &server_cert)
            .with_context(|_| ErrorKind::CertificateConversionError)?;

        Ok(pkcs_certs
            .to_der()
            .with_context(|_| ErrorKind::CertificateConversionError)?)
    }

    pub fn get_stored_cert_bytes(&self) -> Result<String, Error> {
        let stored_cert = self.get_certificate()?;

        Ok(stored_cert.cert)
    }

    fn get_certificate(&self) -> Result<Certificate, Error> {
        // First, try to directly read
        {
            let stored_cert = self
                .certificate
                .read()
                .expect("Locking the certificate for read failed.");

            if let Some(stored_cert) = stored_cert.as_ref() {
                return Ok(stored_cert.clone());
            }
        }

        // No valid cert so must create
        let mut stored_cert = self
            .certificate
            .write()
            .expect("Locking the certificate for write failed.");

        if let Some(stored_cert) = stored_cert.as_ref() {
            Ok(stored_cert.clone())
        } else {
            let created_certificate = self.create_cert()?;
            Ok(stored_cert.get_or_insert(created_certificate).clone())
        }
    }

    fn create_cert(&self) -> Result<Certificate, Error> {
        let cert = self
            .crypto
            .create_certificate(&self.props)
            .with_context(|_| ErrorKind::CertificateCreationError)?;

        let cert_pem = cert
            .pem()
            .with_context(|_| ErrorKind::CertificateCreationError)?;

        let cert_private_key = cert
            .get_private_key()
            .with_context(|_| ErrorKind::CertificateCreationError)?;

        let pk = match cert_private_key {
            Some(pk) => pk,
            None => panic!("Unable to acquire a private key."),
        };

        // Our implementations do not return a ref, and if they did, it would be unusable by Tokio
        // a ref simply is a label/alias to a private key, not the actual bits.
        let pk_bytes = match pk {
            PrivateKey::Ref(_) => panic!(
                "A reference private key does not contain the bits needed for the TLS certificate."
            ),
            PrivateKey::Key(KeyBytes::Pem(k)) => k,
        };

        let cert_str = String::from_utf8(cert_pem.as_ref().to_vec())
            .with_context(|_| ErrorKind::CertificateCreationError)?;

        let key_str = String::from_utf8(pk_bytes.as_bytes().to_vec())
            .with_context(|_| ErrorKind::CertificateCreationError)?;

        Ok(Certificate {
            cert: cert_str,
            private_key: key_str,
        })
    }

    #[cfg(test)]
    fn has_certificate(&self) -> bool {
        !self
            .certificate
            .read()
            .expect("Locking the certificate for read failed.")
            .is_none()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use edgelet_core::crypto::{KeyBytes, PrivateKey};
    use edgelet_core::{CertificateProperties, CertificateType};

    use chrono::{DateTime, Utc};

    use edgelet_core::{
        Certificate as CoreCertificate, CertificateProperties as CoreCertificateProperties,
        CreateCertificate as CoreCreateCertificate, Error as CoreError,
        PrivateKey as CorePrivateKey,
    };

    #[test]
    pub fn test_new_manager_has_no_cert() {
        let crypto = TestCrypto::new().unwrap();

        let edgelet_cert_props = CertificateProperties::new(
            123_456,
            "IOTEDGED_TLS_COMMONNAME".to_string(),
            CertificateType::Server,
            "iotedge-tls".to_string(),
        );

        let manager = CertificateManager::new(crypto.clone(), edgelet_cert_props);

        assert_eq!(manager.has_certificate(), false);
    }

    #[test]
    pub fn test_manager_cert_pem_has_cert() {
        let crypto = TestCrypto::new().unwrap();

        let edgelet_cert_props = CertificateProperties::new(
            123_456,
            "IOTEDGED_TLS_COMMONNAME".to_string(),
            CertificateType::Server,
            "iotedge-tls".to_string(),
        );

        let manager = CertificateManager::new(crypto.clone(), edgelet_cert_props);

        let cert = manager.get_certificate().unwrap();

        assert_eq!(cert.cert, "test".to_string());

        assert_eq!(manager.has_certificate(), true);
    }

    #[derive(Clone)]
    struct TestCrypto {
        created: bool,
    }

    impl TestCrypto {
        pub fn new() -> Result<Self, CoreError> {
            Ok(Self { created: true })
        }
    }

    impl CoreCreateCertificate for TestCrypto {
        type Certificate = TestCertificate;

        fn create_certificate(
            &self,
            _properties: &CoreCertificateProperties,
        ) -> Result<Self::Certificate, CoreError> {
            Ok(TestCertificate {})
        }

        fn destroy_certificate(&self, _alias: String) -> Result<(), CoreError> {
            Ok(())
        }
    }

    struct TestCertificate {}

    impl CoreCertificate for TestCertificate {
        type Buffer = String;
        type KeyBuffer = Vec<u8>;

        fn pem(&self) -> Result<Self::Buffer, CoreError> {
            Ok("test".to_string())
        }

        fn get_private_key(&self) -> Result<Option<CorePrivateKey<Self::KeyBuffer>>, CoreError> {
            Ok(Some(PrivateKey::Key(KeyBytes::Pem(
                "akey".to_string().as_bytes().to_vec(),
            ))))
        }

        fn get_valid_to(&self) -> Result<DateTime<Utc>, CoreError> {
            Ok(DateTime::parse_from_rfc3339("2025-12-19T16:39:57-08:00")
                .unwrap()
                .with_timezone(&Utc))
        }
    }
}
