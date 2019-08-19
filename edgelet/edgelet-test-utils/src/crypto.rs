// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{Error as CoreError, ErrorKind as CoreErrorKind, GetTrustBundle};

use crate::cert::TestCert;

#[derive(Clone, Default, Debug)]
pub struct TestHsm {
    fail_call: bool,
    cert: TestCert,
}

impl TestHsm {
    pub fn with_fail_call(mut self, fail_call: bool) -> Self {
        self.fail_call = fail_call;
        self
    }

    pub fn with_cert(mut self, cert: TestCert) -> Self {
        self.cert = cert;
        self
    }
}

impl GetTrustBundle for TestHsm {
    type Certificate = TestCert;

    fn get_trust_bundle(&self) -> Result<Self::Certificate, CoreError> {
        if self.fail_call {
            Err(CoreError::from(CoreErrorKind::KeyStore))
        } else {
            Ok(self.cert.clone())
        }
    }
}
