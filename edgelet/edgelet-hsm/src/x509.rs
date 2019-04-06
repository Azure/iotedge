
pub use::hsm{Buffer};
use hsm::{X509 as HsmX509};

use edgelet_core::{
    Certificate as CoreCertificate, Error as CoreError,
    GetDeviceIdentityCertificate as CoreGetDeviceIdentityCertificate);

/// The X.509 device identity HSM instance
#[derive(Clone)]
pub struct X509 {
    x509: Arc<Mutex<HsmX509>>,
}

impl X509 {
    pub fn new() -> Result<Self, Error> {
        let hsm = HsmX509::new()?;
        DeviceIdentityX509::from_hsm(hsm)
    }

    pub fn from_hsm(crypto: HsmCrypto) -> Result<Self, Error> {
        Ok(DeviceIdentityX509 {
            x509: Arc::new(Mutex::new(x509)),
        })
    }
}

impl CoreGetDeviceIdentityCertificate for X509 {
    type Certificate = Certificate;
    type Buffer = Buffer;

    fn get(&self) -> Result<Self::Certificate, CoreError> {
        let cert = self
            .x509
            .lock()
            .expect("Lock on X509 structure failed")
            .get_certificate_info()
            .map_err(|err| Error::from(err.context(ErrorKind::Hsm)))
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))?;
        Ok(Certificate(cert))
    }

    fn sign_with_private_key(&self, data: &[u8]) -> Result<Self::Buffer, Error>
    {
        let cert = self
            .x509
            .lock()
            .expect("Lock on X509 structure failed")
            .sign_with_private_key()
            .map_err(|err| CoreError::from(err.context(CoreErrorKind::KeyStore)))
    }
}