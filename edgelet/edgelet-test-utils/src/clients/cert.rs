// Copyright (c) Microsoft. All rights reserved.

#[derive(Default)]
pub struct CertClient {
    pub certs: std::collections::BTreeMap<String, Vec<u8>>,
}

impl CertClient {
    pub async fn create_cert(
        &self,
        id: &str,
        csr: &[u8],
        issuer: Option<(&str, &aziot_key_common::KeyHandle)>,
    ) -> Result<Vec<u8>, std::io::Error> {
        todo!()
    }

    pub async fn get_cert(&self, id: &str) -> Result<Vec<u8>, std::io::Error> {
        match self.certs.get(id) {
            Some(cert) => Ok(cert.clone()),
            None => Err(crate::test_error()),
        }
    }
}
