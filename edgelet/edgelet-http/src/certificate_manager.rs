#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use std::sync::{Arc, RwLock};
use std::time::{Duration, Instant};

use futures::future::Either;
#[cfg(unix)]
use openssl::pkcs12::Pkcs12;
#[cfg(unix)]
use openssl::pkey::PKey;
#[cfg(unix)]
use openssl::stack::Stack;
#[cfg(unix)]
use openssl::x509::X509;
use tokio::prelude::*;
use tokio::timer::Delay;

use edgelet_core::crypto::{
    Certificate as CryptoCertificate, CreateCertificate, KeyBytes, PrivateKey, Signature,
};
use edgelet_core::CertificateProperties;
use failure::ResultExt;

pub use crate::error::{Error, ErrorKind};

pub struct CertificateManager<C: CreateCertificate + Clone> {
    certificate: Arc<RwLock<Option<Certificate>>>,
    crypto: C,
    props: CertificateProperties,
    creation_time: Instant,
}

#[derive(Clone)]
struct Certificate {
    cert: String,
    private_key: String,
}

impl<C: CreateCertificate + Clone> CertificateManager<C> {
    pub fn new(crypto: C, props: CertificateProperties) -> Result<Self, Error> {
        let cert_manager = Self {
            certificate: Arc::new(RwLock::new(None)),
            crypto,
            props,
            creation_time: Instant::now(),
        };

        {
            let mut cert = cert_manager
                .certificate
                .write()
                .expect("Locking the certificate for write failed.");

            let created_certificate = cert_manager.create_cert()?;

            *cert = Some(created_certificate);
        }

        Ok(cert_manager)
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

    pub fn schedule_expiration_timer<F>(
        &self,
        expiration_callback: F,
    ) -> impl Future<Item = (), Error = Error>
    where
        F: FnOnce() -> Result<(), ()> + Sync + Send + 'static,
    {
        // Now, let's set a timer to expire this certificate
        // expire the certificate with 2 minutes remaining in it's lifetime
        let when = self.compute_certificate_alarm_time();

        // Fail if the cert has already been expired when the call to create
        // a timer happens.
        if when < (Instant::now() + Duration::from_secs(1)) {
            Either::A(future::err(Error::from(
                ErrorKind::CertificateTimerCreationError,
            )))
        } else {
            Either::B(
                Delay::new(when)
                    .map_err(|_| Error::from(ErrorKind::CertificateTimerCreationError))
                    .and_then(move |_| match expiration_callback() {
                        Ok(_) => Ok(()),
                        Err(_) => Err(Error::from(ErrorKind::CertificateTimerRuntimeError)),
                    }),
            )
        }
    }

    fn get_certificate(&self) -> Result<Certificate, Error> {
        // Try to directly read
        let stored_cert = self
            .certificate
            .read()
            .expect("Locking the certificate for read failed.");

        match stored_cert.as_ref() {
            Some(stored_cert) => Ok(stored_cert.clone()),
            None => Err(Error::from(ErrorKind::CertificateNotFound)),
        }
    }

    fn create_cert(&self) -> Result<Certificate, Error> {
        // In some use cases, the CA cert might change - to protect against that,
        // we will retry once (after attempting to delete) if the cert creation fails.
        let cert = if let Ok(val) = self.crypto.create_certificate(&self.props) {
            val
        } else {
            self.crypto
                .destroy_certificate(self.props.alias().to_string())
                .with_context(|_| ErrorKind::CertificateDeletionError)?;
            self.crypto
                .create_certificate(&self.props)
                .with_context(|_| ErrorKind::CertificateCreationError)?
        };

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

    // Determine when to sound the alarm and renew the certificate.
    #[allow(clippy::cast_possible_truncation)]
    #[allow(clippy::cast_sign_loss)]
    #[allow(clippy::cast_precision_loss)]
    fn compute_certificate_alarm_time(&self) -> Instant {
        self.creation_time
            + Duration::from_secs((*self.props.validity_in_secs() as f64 * 0.95) as u64)
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
    pub fn test_cert_manager_pem_has_cert() {
        let crypto = TestCrypto::new().unwrap();

        let edgelet_cert_props = CertificateProperties::new(
            123_456,
            "IOTEDGED_TLS_COMMONNAME".to_string(),
            CertificateType::Server,
            "iotedge-tls".to_string(),
        );

        let manager = CertificateManager::new(crypto, edgelet_cert_props).unwrap();

        let cert = manager.get_certificate().unwrap();

        assert_eq!(cert.cert, "test".to_string());

        assert_eq!(manager.has_certificate(), true);
    }

    #[test]
    pub fn test_cert_manager_expired_timer_creation() {
        let crypto = TestCrypto::new().unwrap();

        let edgelet_cert_props = CertificateProperties::new(
            1, // 150 second validity
            "IOTEDGED_TLS_COMMONNAME".to_string(),
            CertificateType::Server,
            "iotedge-tls".to_string(),
        );

        let manager = CertificateManager::new(crypto, edgelet_cert_props).unwrap();
        let _timer = manager.schedule_expiration_timer(|| Ok(()));
    }

    #[test]
    pub fn test_cert_manager_expired_timer_creation_fails() {
        let crypto = TestCrypto::new().unwrap();

        let edgelet_cert_props = CertificateProperties::new(
            50, // 50 second validity
            "IOTEDGED_TLS_COMMONNAME".to_string(),
            CertificateType::Server,
            "iotedge-tls".to_string(),
        );

        let manager = CertificateManager::new(crypto, edgelet_cert_props).unwrap();

        let timer = manager.schedule_expiration_timer(|| Ok(())).wait();
        match timer {
            Ok(_) => panic!("Should not be okay to create this timer..."),
            Err(err) => {
                if let ErrorKind::CertificateTimerCreationError = err.kind() {
                    assert_eq!(true, true);
                } else {
                    panic!(
                        "Expected a CertificteTimerCreationError type, but got {:?}",
                        err
                    );
                }
            }
        }
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

        fn get_certificate(&self, _alias: String) -> Result<Self::Certificate, CoreError> {
            Ok(TestCertificate {})
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

        fn get_common_name(&self) -> Result<String, CoreError> {
            Ok("IOTEDGED_TLS_COMMONNAME".to_string())
        }
    }
}
