// Copyright (c) Microsoft. All rights reserved.

use chrono::{DateTime, Utc};
use edgelet_core::{Certificate, Error as CoreError, ErrorKind as CoreErrorKind, PrivateKey};

#[derive(Clone, Debug, Default)]
#[allow(clippy::struct_excessive_bools)]
pub struct TestCert {
    cert: Vec<u8>,
    fail_pem: bool,
    private_key: Option<PrivateKey<String>>,
    fail_private_key: bool,
    fail_valid_to: bool,
    valid_to: Option<DateTime<Utc>>,
    common_name: String,
    fail_common_name: bool,
}

impl TestCert {
    pub fn with_cert(mut self, cert: Vec<u8>) -> Self {
        self.cert = cert;
        self
    }

    pub fn with_fail_pem(mut self, fail_pem: bool) -> Self {
        self.fail_pem = fail_pem;
        self
    }

    pub fn with_private_key(mut self, private_key: PrivateKey<String>) -> Self {
        self.private_key = Some(private_key);
        self
    }

    pub fn with_fail_private_key(mut self, fail_private_key: bool) -> Self {
        self.fail_private_key = fail_private_key;
        self
    }

    pub fn with_valid_to(mut self, valid_to: DateTime<Utc>) -> Self {
        self.fail_valid_to = false;
        self.valid_to = Some(valid_to);
        self
    }

    pub fn with_fail_valid_to(mut self, fail_valid_to: bool) -> Self {
        self.fail_valid_to = fail_valid_to;
        self
    }

    pub fn with_common_name(mut self, common_name: String) -> Self {
        self.common_name = common_name;
        self
    }

    pub fn with_fail_common_name(mut self, fail_common_name: bool) -> Self {
        self.fail_common_name = fail_common_name;
        self
    }
}

impl Certificate for TestCert {
    type Buffer = Vec<u8>;
    type KeyBuffer = String;

    fn pem(&self) -> Result<Vec<u8>, CoreError> {
        if self.fail_pem {
            Err(CoreError::from(CoreErrorKind::KeyStore))
        } else {
            Ok(self.cert.clone())
        }
    }

    fn get_private_key(&self) -> Result<Option<PrivateKey<Self::KeyBuffer>>, CoreError> {
        if self.fail_private_key {
            Err(CoreError::from(CoreErrorKind::KeyStore))
        } else {
            Ok(Some(self.private_key.as_ref().cloned().unwrap()))
        }
    }

    fn get_valid_to(&self) -> Result<DateTime<Utc>, CoreError> {
        if self.fail_valid_to {
            Err(CoreError::from(CoreErrorKind::KeyStore))
        } else {
            match self.valid_to {
                Some(ts) => Ok(ts),
                None => Ok(Utc::now()),
            }
        }
    }

    fn get_common_name(&self) -> Result<String, CoreError> {
        if self.fail_common_name {
            Err(CoreError::from(CoreErrorKind::KeyStore))
        } else {
            Ok(self.common_name.clone())
        }
    }
}
