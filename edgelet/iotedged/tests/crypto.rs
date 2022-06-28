// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::crypto::MemoryKey;
use edgelet_core::{
    CertificateProperties, CreateCertificate, Decrypt, Encrypt, GetTrustBundle, KeyBytes,
    KeyIdentity, KeyStore, MasterEncryptionKey, PrivateKey,
};
use edgelet_test_utils::cert::TestCert;

#[derive(Clone, Default, Debug)]
pub struct TestCrypto;

impl GetTrustBundle for TestCrypto {
    type Certificate = TestCert;

    fn get_trust_bundle(&self) -> Result<Self::Certificate, edgelet_core::Error> {
        unimplemented!()
    }
}

impl MasterEncryptionKey for TestCrypto {
    fn create_key(&self) -> Result<(), edgelet_core::Error> {
        unimplemented!();
    }
    fn destroy_key(&self) -> Result<(), edgelet_core::Error> {
        unimplemented!();
    }
}

impl CreateCertificate for TestCrypto {
    type Certificate = TestCert;

    fn create_certificate(
        &self,
        _properties: &CertificateProperties,
    ) -> Result<Self::Certificate, edgelet_core::Error> {
        Ok(TestCert::default()
            .with_cert(vec![1, 2, 3])
            .with_private_key(PrivateKey::Key(KeyBytes::Pem("some key".to_string())))
            .with_fail_pem(false)
            .with_fail_private_key(false))
    }

    fn destroy_certificate(&self, _alias: String) -> Result<(), edgelet_core::Error> {
        unimplemented!()
    }

    fn get_certificate(&self, _alias: String) -> Result<Self::Certificate, edgelet_core::Error> {
        unimplemented!()
    }
}

impl Encrypt for TestCrypto {
    type Buffer = Vec<u8>;

    fn encrypt(
        &self,
        _client_id: &[u8],
        _plaintext: &[u8],
        _initialization_vector: &[u8],
    ) -> Result<Self::Buffer, edgelet_core::Error> {
        unimplemented!()
    }
}

impl Decrypt for TestCrypto {
    // type Buffer = Buffer;
    type Buffer = Vec<u8>;

    fn decrypt(
        &self,
        _client_id: &[u8],
        _ciphertext: &[u8],
        _initialization_vector: &[u8],
    ) -> Result<Self::Buffer, edgelet_core::Error> {
        unimplemented!()
    }
}

#[derive(Clone, Default, Debug)]
pub struct TestKeyStore;

impl KeyStore for TestKeyStore {
    type Key = MemoryKey;

    fn get(
        &self,
        _identity: &KeyIdentity,
        _key_name: &str,
    ) -> Result<Self::Key, edgelet_core::Error> {
        unimplemented!()
    }
}

pub struct TestCertificateManager<C: CreateCertificate + Clone> {
    _crypto: C,
}

impl<C: CreateCertificate + Clone> TestCertificateManager<C> {
    pub fn new(crypto: C) -> Self {
        Self { _crypto: crypto }
    }
}
