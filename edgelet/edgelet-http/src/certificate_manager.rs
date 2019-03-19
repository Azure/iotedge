use std::sync::{Arc, RwLock};

use edgelet_core::crypto::{Certificate, CreateCertificate};
use edgelet_core::CertificateProperties;
use failure::ResultExt;

pub use crate::error::{Error, ErrorKind};

#[derive(Clone)]
pub struct CertificateManager<C: CreateCertificate + Clone> {
    certificate: Arc<RwLock<Option<String>>>,
    crypto: C,
    props: CertificateProperties,
}

impl<C: CreateCertificate + Clone> CertificateManager<C> {
    pub fn new(crypto: C, props: CertificateProperties) -> Self {
        CertificateManager {
            certificate: Arc::new(RwLock::new(None)),
            crypto,
            props,
        }
    }

    pub fn get_certificate(&self) -> Result<String, Error> {
        // First, try to directly read
        {
            let cert = self
                .certificate
                .read()
                .expect("Locking the certificate for read failed.");

            if let Some(cert) = cert.as_ref() {
                return Ok(cert.to_string());
            }
        }

        // No valid cert so must create
        let mut cert = self
            .certificate
            .write()
            .expect("Locking the certificate for write failed.");

        if let Some(cert) = cert.as_ref() {
            Ok(cert.to_string())
        } else {
            let new_cert = self
                .create_cert()
                .with_context(|_| ErrorKind::CertificateCreationError)?;
            Ok(cert.get_or_insert(new_cert).to_string())
        }
    }

    fn create_cert(&self) -> Result<String, Error> {
        let cert = self
            .crypto
            .create_certificate(&self.props)
            .with_context(|_| ErrorKind::CertificateCreationError)?;

        let cert_pem = cert
            .pem()
            .with_context(|_| ErrorKind::CertificateCreationError)?;

        let cert_str = String::from_utf8(cert_pem.as_ref().to_vec())
            .with_context(|_| ErrorKind::CertificateCreationError)?;

        Ok(cert_str)
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
            1_234_56,
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
            1_234_56,
            "IOTEDGED_TLS_COMMONNAME".to_string(),
            CertificateType::Server,
            "iotedge-tls".to_string(),
        );

        let manager = CertificateManager::new(crypto.clone(), edgelet_cert_props);

        let cert = manager.get_certificate().unwrap();

        assert_eq!(cert, "test".to_string());

        assert_eq!(manager.has_certificate(), true);
    }

    #[derive(Clone)]
    struct TestCrypto {
        created: bool,
    }

    impl TestCrypto {
        pub fn new() -> Result<Self, CoreError> {
            Ok(TestCrypto { created: true })
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
            Ok(None)
        }

        fn get_valid_to(&self) -> Result<DateTime<Utc>, CoreError> {
            Ok(DateTime::parse_from_rfc3339("2025-12-19T16:39:57-08:00")
                .unwrap()
                .with_timezone(&Utc))
        }
    }
}
