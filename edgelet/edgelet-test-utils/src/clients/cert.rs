// Copyright (c) Microsoft. All rights reserved.

type Certs =
    futures_util::lock::Mutex<std::cell::RefCell<std::collections::BTreeMap<String, Vec<u8>>>>;

#[derive(Default)]
pub struct CertClient {
    pub certs: Certs,
}

impl CertClient {
    pub async fn create_cert(
        &self,
        id: &str,
        csr: &[u8],
        _issuer: Option<(&str, &aziot_key_common::KeyHandle)>,
    ) -> Result<Vec<u8>, std::io::Error> {
        // A real cert client would connect to certd and create the cert.
        // This test client just places the provided CSR in the cert map so that
        // the tester can check if the cert would be created.
        let certs = self.certs.lock().await;
        certs.replace_with(|certs| {
            certs.insert(id.to_string(), csr.to_owned());

            certs.to_owned()
        });

        Ok(csr.to_owned())
    }

    pub async fn get_cert(&self, id: &str) -> Result<Vec<u8>, std::io::Error> {
        let certs = self.certs.lock().await;
        let certs = certs.replace_with(|certs| certs.clone());

        match certs.get(id) {
            Some(cert) => Ok(cert.clone()),
            None => Err(crate::test_error()),
        }
    }
}
