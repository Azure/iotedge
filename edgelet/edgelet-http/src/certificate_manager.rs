use std::str;
use std::sync::{Arc, RwLock};

use edgelet_core::crypto::{Certificate, CreateCertificate};
use edgelet_core::{CertificateIssuer, CertificateProperties, CertificateType};
use failure::ResultExt;

pub use crate::error::{Error, ErrorKind};

const IOTEDGED_VALIDITY: u64 = 7_776_000; // 90 days
const IOTEDGED_TLS_COMMONNAME: &str = "iotedge tls";

#[derive(Clone)]
pub struct CertificateManager<C: CreateCertificate + Clone> {
    certificate: Arc<RwLock<String>>,
    crypto: C,
}

impl<C: CreateCertificate + Clone> CertificateManager<C> {
    pub fn new(crypto_struct: C) -> Result<Self, Error> {
        Ok(CertificateManager {
            certificate: Arc::new(RwLock::new("".to_string())),
            crypto: crypto_struct,
        })
    }

    pub fn get_certificate(&self) -> Result<String, Error> {
        // First, try to directly read
        {
            let cert = self
                .certificate
                .read()
                .expect("Locking the certificate for read failed.")
                .to_string();

            if cert.len() > 0 {
                return Ok(cert);
            }
        }

        // No valid cert so must create
        let mut cert = self
            .certificate
            .write()
            .expect("Locking the certificate for write failed.");

        // Check that another thread hasn't already created one for us
        if cert.to_string().len() > 0 {
            return Ok(cert.to_string());
        }

        let new_cert = self.create_cert();

        // Assign and return new cert
        *cert = new_cert.with_context(|_| ErrorKind::CertificateCreationError)?;

        Ok(cert.to_string())
    }

    fn create_cert(&self) -> Result<String, Error> {
        let edgelet_cert_props = CertificateProperties::new(
            IOTEDGED_VALIDITY,
            IOTEDGED_TLS_COMMONNAME.to_string(),
            CertificateType::Server,
            "iotedge-tls".to_string(),
        )
        .with_issuer(CertificateIssuer::DeviceCa);

        let cert = self
            .crypto
            .create_certificate(&edgelet_cert_props)
            .with_context(|_| ErrorKind::CertificateCreationError)?;

        let cert_pem = cert
            .pem()
            .with_context(|_| ErrorKind::CertificateCreationError)?;

        let cert_str = String::from_utf8(cert_pem.as_ref().to_vec())
            .with_context(|_| ErrorKind::CertificateCreationError)?;

        Ok(cert_str)
    }

    #[allow(dead_code)]
    // Test helper
    fn has_certificate(&self) -> bool {
        self.certificate
            .read()
            .expect("Locking the certificate for read failed.")
            .len()
            > 0
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use chrono::{DateTime, Utc};

    use edgelet_core::{
        Certificate as CoreCertificate, CertificateProperties as CoreCertificateProperties,
        CreateCertificate as CoreCreateCertificate, Error as CoreError,
        PrivateKey as CorePrivateKey,
    };

    #[test]
    pub fn test_new_manager_has_no_cert() {
        let crypto = TestCrypto::new().unwrap();

        let manager = CertificateManager::new(crypto.clone()).unwrap();

        assert_eq!(manager.has_certificate(), false);
    }

    #[test]
    pub fn test_manager_cert_pem_has_cert() {
        let crypto = TestCrypto::new().unwrap();

        let manager = CertificateManager::new(crypto.clone()).unwrap();

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
