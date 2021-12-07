// Copyright (c) Microsoft. All rights reserved.

pub struct CertClient {
    // Because the signatures of this test client must match the real client, the test client's
    // functions cannot use `mut self` as a parameter.
    //
    // The test client may need to mutate this map of certs, so the workaround is to place it in
    // a RefCell and use replace_with.
    pub certs:
        futures_util::lock::Mutex<std::cell::RefCell<std::collections::BTreeMap<String, Vec<u8>>>>,

    pub issuer: openssl::x509::X509,
    pub issuer_key: openssl::pkey::PKey<openssl::pkey::Private>,
}

impl Default for CertClient {
    fn default() -> Self {
        let (issuer, issuer_key) = crate::test_certificate("test-device-cert", None);

        CertClient {
            certs: Default::default(),
            issuer,
            issuer_key,
        }
    }
}

impl CertClient {
    // For the real cert client, the 3rd parameter `data` is always a CSR to send to certd.
    // This test client repurposes this parameter:
    // - As cert bytes when no issuer is provided
    // - As CSR bytes when an issuer is provided
    pub async fn create_cert(
        &self,
        id: &str,
        data: &[u8],
        issuer: Option<(&str, &aziot_key_common::KeyHandle)>,
    ) -> Result<Vec<u8>, std::io::Error> {
        // The real cert client would connect to certd and create the cert.
        // This test client just issues certs locally if an issuer is provided.
        // Otherwise, it just places the provided data into the map of certs.
        let cert = if issuer.is_none() {
            data.to_owned()
        } else {
            self.issue_cert(data)
        };

        let certs = self.certs.lock().await;
        certs.replace_with(|certs| {
            certs.insert(id.to_string(), cert.clone());

            certs.to_owned()
        });

        Ok(cert)
    }

    pub async fn get_cert(&self, id: &str) -> Result<Vec<u8>, std::io::Error> {
        let certs = self.certs.lock().await;
        let certs = certs.replace_with(|certs| certs.clone());

        match certs.get(id) {
            Some(cert) => Ok(cert.clone()),
            None => Err(crate::test_error()),
        }
    }

    fn issue_cert(&self, csr: &[u8]) -> Vec<u8> {
        let mut issuer_name = openssl::x509::X509Name::builder().unwrap();
        issuer_name
            .append_entry_by_text("CN", "test-device-cert")
            .unwrap();
        let issuer_name = issuer_name.build();

        let csr = openssl::x509::X509Req::from_pem(csr).unwrap();
        let csr_pubkey = csr.public_key().unwrap();
        let csr_extensions = csr.extensions().unwrap();
        assert!(csr.verify(&csr_pubkey).unwrap());

        let mut cert = openssl::x509::X509::builder().unwrap();
        cert.set_subject_name(csr.subject_name()).unwrap();
        cert.set_issuer_name(&issuer_name).unwrap();
        cert.set_pubkey(&csr_pubkey).unwrap();

        for extension in csr_extensions.iter() {
            cert.append_extension2(extension).unwrap();
        }

        let not_before = openssl::asn1::Asn1Time::from_unix(0).unwrap();
        let not_after = openssl::asn1::Asn1Time::days_from_now(30).unwrap();

        cert.set_not_before(&not_before).unwrap();
        cert.set_not_after(&not_after).unwrap();

        cert.sign(&self.issuer_key, openssl::hash::MessageDigest::sha256())
            .unwrap();

        cert.build().to_pem().unwrap()
    }
}
