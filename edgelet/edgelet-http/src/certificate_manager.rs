use std::sync::{Arc, RwLock};

use edgelet_hsm::Crypto;
use edgelet_core::Certificate;

#[allow(dead_code)]
pub struct CertificateManager { 
    certificate: RwLock<Arc<Certificate>>,
}

#[allow(dead_code)]
impl CertificateManager {
    // Eventually will take a crypto.clone()
    pub fn new(cert: Certificate) -> Self { 
        CertificateManager {
            certificate: RwLock::new(Arc::new(cert)),
        }
    }

    pub fn get_certificate(&self) -> String {
        let cert = *self.certificate
            .read()
            .expect("Error reading certificate")
            .clone();
        cert.pem()

    }
}