use std::sync::Arc;

use failure::Fail;

use hsm::{
    GetDeviceIdentityCertificate as HsmGetDeviceIdentityCertificate,
    PrivateKeySignDigest as HsmPrivateKeySignDigest, X509 as HsmX509,
};

use crate::crypto::Certificate;
pub use crate::error::{Error, ErrorKind};
use crate::HsmLock;

use edgelet_core::{
    Error as CoreError, ErrorKind as CoreErrorKind,
    GetDeviceIdentityCertificate as CoreGetDeviceIdentityCertificate,
    GetHsmVersion as CoreGetHsmVersion,
};

/// The X.509 device identity HSM instance
#[derive(Clone)]
pub struct X509 {
    x509: Arc<HsmX509>,
    hsm_lock: Arc<HsmLock>,
}

// HsmX509 is Send and !Sync. However X509 can be Sync since all access to X509::x509
// is controlled by the methods of X509, which all lock X509::hsm_lock first.
//
// For the same reason, X509 also needs an explicit Send impl
// since Arc<T>: Send requires T: Send + Sync.
unsafe impl Send for X509 {}
unsafe impl Sync for X509 {}

impl X509 {
    pub fn new(hsm_lock: Arc<HsmLock>, auto_generated_ca_validity: u64) -> Result<Self, Error> {
        let hsm = HsmX509::new(auto_generated_ca_validity)?;
        X509::from_hsm(hsm, hsm_lock)
    }

    pub fn from_hsm(x509: HsmX509, hsm_lock: Arc<HsmLock>) -> Result<Self, Error> {
        Ok(X509 {
            x509: Arc::new(x509),
            hsm_lock,
        })
    }
}

impl CoreGetHsmVersion for X509 {
    fn get_version(&self) -> Result<String, CoreError> {
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        self.x509
            .get_version()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::HsmVersion)))
    }
}

impl CoreGetDeviceIdentityCertificate for X509 {
    type Certificate = Certificate;
    type Buffer = HsmPrivateKeySignDigest;

    fn get(&self) -> Result<Self::Certificate, CoreError> {
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        let cert = self
            .x509
            .get_certificate_info()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| {
                CoreError::from(err.context(CoreErrorKind::DeviceIdentityCertificate))
            })?;
        Ok(Certificate::new(cert))
    }

    fn sign_with_private_key(&self, data: &[u8]) -> Result<Self::Buffer, CoreError> {
        let _hsm_lock = self.hsm_lock.0.lock().expect("Acquiring HSM lock failed");
        self.x509
            .sign_with_private_key(data)
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::DeviceIdentitySign)))
    }
}
