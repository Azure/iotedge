
use std::str;

use edgelet_core::crypto::{Certificate, CreateCertificate};
use edgelet_core::{CertificateIssuer, CertificateProperties, CertificateType};

const IOTEDGED_VALIDITY: u64 = 7_776_000; // 90 days
const IOTEDGED_TLS_COMMONNAME: &str = "iotedge tls";

#[allow(dead_code)]
#[derive(Clone)]
pub struct CertificateManager<C: CreateCertificate + Clone> { 
    // Add Arc?
    certificate: String,
    crypto: C,
}


#[allow(dead_code)]
impl<C: CreateCertificate + Clone> CertificateManager<C> {

    pub fn new(crypto_struct: C) -> Self
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
            .unwrap();

        let cert_pem = cert.pem().unwrap();

        let cert_str = String::from_utf8(cert_pem.as_ref().to_vec()).unwrap().to_string();

        CertificateManager {
            certificate: cert_str,
            crypto: crypto_struct,
        }
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
    //use edgelet_test_utils::cert::TestCert;

    #[test]
    pub fn test_fake() {
        assert_eq!(true, true);
    }
}