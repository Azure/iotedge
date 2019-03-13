
use std::str;

use edgelet_core::crypto::{Certificate, CreateCertificate};
use edgelet_core::{CertificateIssuer, CertificateProperties, CertificateType};
use failure::{ResultExt};

pub use crate::error::{Error, ErrorKind};

const IOTEDGED_VALIDITY: u64 = 7_776_000; // 90 days
const IOTEDGED_TLS_COMMONNAME: &str = "iotedge tls";

#[allow(dead_code)]
#[derive(Clone)]
pub struct CertificateManager<C: CreateCertificate + Clone> { 
    certificate: String,
    crypto: C,
}


#[allow(dead_code)]
impl<C: CreateCertificate + Clone> CertificateManager<C> {

    pub fn new(crypto_struct: C) -> Result<Self, Error>
    { 

        let edgelet_cert_props = CertificateProperties::new(
            IOTEDGED_VALIDITY,
            IOTEDGED_TLS_COMMONNAME.to_string(),
            CertificateType::Server,
            "iotedge-tls".to_string(),
        )
        .with_issuer(CertificateIssuer::DeviceCa);

        let cert = crypto_struct
            .create_certificate(&edgelet_cert_props)
            .with_context(|_| {
                ErrorKind::CertificateCreationError
            })?;

        let cert_pem = cert.pem()
            .with_context(|_| {
                ErrorKind::CertificateCreationError
            })?;

        let cert_str = String::from_utf8(cert_pem.as_ref().to_vec())
            .with_context(|_| {
                ErrorKind::CertificateCreationError
            })?;

        Ok(CertificateManager {
            certificate: cert_str.to_string(),
            crypto: crypto_struct,
        })
    }

    pub fn get_certificate(&self) -> String {
        self.certificate.clone()
    }

    pub fn has_cert(&self) -> bool {
        self.certificate.len() > 0
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use chrono::{DateTime, Utc};

    use edgelet_core::{
        Certificate as CoreCertificate, CertificateProperties as CoreCertificateProperties,
        CreateCertificate as CoreCreateCertificate,
        Error as CoreError, 
        PrivateKey as CorePrivateKey,
    };


    #[test]
    pub fn test_manager_has_cert() {
        let crypto = TestCrypto::new().unwrap();

        let manager = CertificateManager::new(crypto.clone()).unwrap();

        assert_eq!(manager.has_cert(), true);
    }

    #[test]
    pub fn test_manager_cert_pem() {
        let crypto = TestCrypto::new().unwrap();

        let manager = CertificateManager::new(crypto.clone()).unwrap();

        assert_eq!(manager.get_certificate(), "test".to_string());
    }

    #[derive(Clone)]
    struct TestCrypto {
        created: bool,
    }

    impl TestCrypto
    {
        pub fn new() -> Result<Self, CoreError> {
            Ok(TestCrypto{
                created: true
            })
        }
    }

    impl CoreCreateCertificate for TestCrypto {
        type Certificate = TestCertificate;

        fn create_certificate(
            &self,
            _properties: &CoreCertificateProperties,
        ) -> Result<Self::Certificate, CoreError> {

            Ok(TestCertificate{})
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
            Ok(DateTime::parse_from_rfc3339("2025-12-19T16:39:57-08:00").unwrap().with_timezone(&Utc))
        }
    }
}