// Copyright (c) Microsoft. All rights reserved.

pub struct CertClient {}

impl Default for CertClient {
    fn default() -> Self {
        CertClient {}
    }
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
        todo!()
    }
}
