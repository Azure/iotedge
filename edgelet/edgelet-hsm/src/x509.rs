use std::sync::{Arc, Mutex};

use failure::Fail;

use hsm::{
    GetDeviceIdentityCertificate as HsmGetDeviceIdentityCertificate,
    PrivateKeySignDigest as HsmPrivateKeySignDigest, X509 as HsmX509,
};

use crate::crypto::Certificate;
pub use crate::error::{Error, ErrorKind};

use edgelet_core::{
    Error as CoreError, ErrorKind as CoreErrorKind,
    GetDeviceIdentityCertificate as CoreGetDeviceIdentityCertificate,
};

/// The X.509 device identity HSM instance
#[derive(Clone)]
pub struct X509 {
    x509: Arc<Mutex<HsmX509>>,
}

impl X509 {
    pub fn new() -> Result<Self, Error> {
        let hsm = HsmX509::new()?;
        X509::from_hsm(hsm)
    }

    pub fn from_hsm(x509: HsmX509) -> Result<Self, Error> {
        Ok(X509 {
            x509: Arc::new(Mutex::new(x509)),
        })
    }
}

impl CoreGetDeviceIdentityCertificate for X509 {
    type Certificate = Certificate;
    type Buffer = HsmPrivateKeySignDigest;

    fn get(&self) -> Result<Self::Certificate, CoreError> {
        let cert = self
            .x509
            .lock()
            .expect("Lock on X509 structure failed")
            .get_certificate_info()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| {
                CoreError::from(err.context(CoreErrorKind::DeviceIdentityCertificate))
            })?;
        Ok(Certificate::new(cert))
    }

    fn sign_with_private_key(&self, data: &[u8]) -> Result<Self::Buffer, CoreError> {
        self.x509
            .lock()
            .expect("Lock on X509 structure failed")
            .sign_with_private_key(data)
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::DeviceIdentitySign)))
    }
}
