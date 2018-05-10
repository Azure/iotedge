// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{Certificate, Error as CoreError, ErrorKind as CoreErrorKind, PrivateKey};

#[derive(Clone, Debug, Default)]
pub struct TestCert {
    cert: Vec<u8>,
    fail_pem: bool,
    private_key: Option<PrivateKey<String>>,
    fail_private_key: bool,
}

impl TestCert {
    pub fn with_cert(mut self, cert: Vec<u8>) -> TestCert {
        self.cert = cert;
        self
    }

    pub fn with_fail_pem(mut self, fail_pem: bool) -> TestCert {
        self.fail_pem = fail_pem;
        self
    }

    pub fn with_private_key(mut self, private_key: PrivateKey<String>) -> TestCert {
        self.private_key = Some(private_key);
        self
    }

    pub fn with_fail_private_key(mut self, fail_private_key: bool) -> TestCert {
        self.fail_private_key = fail_private_key;
        self
    }
}

impl Certificate for TestCert {
    type Buffer = Vec<u8>;
    type KeyBuffer = String;

    fn pem(&self) -> Result<Vec<u8>, CoreError> {
        if self.fail_pem {
            Err(CoreError::from(CoreErrorKind::Io))
        } else {
            Ok(self.cert.clone())
        }
    }

    fn get_private_key(&self) -> Result<Option<PrivateKey<Self::KeyBuffer>>, CoreError> {
        if self.fail_private_key {
            Err(CoreError::from(CoreErrorKind::Io))
        } else {
            Ok(Some(self.private_key.as_ref().cloned().unwrap()))
        }
    }
}
