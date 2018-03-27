// Copyright (c) Microsoft. All rights reserved.

// TODO remove me when warnings are cleaned up
#![allow(warnings)]
use std::collections::HashMap;

use bytes::Bytes;
use consistenttime::ct_u8_slice_eq;
use failure::ResultExt;
use hmac::{Hmac, Mac};
use sha2::Sha256;

use error::{Error, ErrorKind};

pub trait Sign {
    fn sign(
        &self,
        signature_algorithm: SignatureAlgorithm,
        data: &[u8],
    ) -> Result<Signature, Error>;
}

pub enum SignatureAlgorithm {
    HMACSHA256,
}

// TODO add cryptographically secure equal
#[derive(Debug)]
pub struct Signature {
    bytes: Bytes,
}

impl PartialEq for Signature {
    fn eq(&self, other: &Signature) -> bool {
        ct_u8_slice_eq(self.bytes.as_ref(), other.bytes.as_ref())
    }
}

impl Signature {
    pub fn new(bytes: Bytes) -> Signature {
        Signature { bytes }
    }

    pub fn as_bytes(&self) -> &[u8] {
        self.bytes.as_ref()
    }
}

#[derive(Debug)]
pub struct InMemoryKey {
    key: Bytes,
}

impl Sign for InMemoryKey {
    fn sign(
        &self,
        signature_algorithm: SignatureAlgorithm,
        data: &[u8],
    ) -> Result<Signature, Error> {
        let signature = match signature_algorithm {
            SignatureAlgorithm::HMACSHA256 => {
                // Create `Mac` trait implementation, namely HMAC-SHA256
                let mut mac =
                    Hmac::<Sha256>::new(&self.key).map_err(|_| ErrorKind::Sign(self.key.len()))?;
                mac.input(data);

                // `result` has type `MacResult` which is a thin wrapper around array of
                // bytes for providing constant time equality check
                let result = mac.result();
                // To get underlying array use `code` method, but be careful, since
                // incorrect use of the code value may permit timing attacks which defeat
                // the security provided by the `MacResult` (https://docs.rs/hmac/0.5.0/hmac/)
                let code_bytes = result.code();

                Signature::new(Bytes::from(code_bytes.as_ref()))
            }
        };
        Ok(signature)
    }
}

pub trait KeyStore {
    type Key: Sign;

    fn get(&self, identity: &str, key_name: &str) -> Option<&Self::Key>;
}

pub struct MemoryKeyStore {
    keys: HashMap<String, InMemoryKey>,
}

impl MemoryKeyStore {
    pub fn new() -> MemoryKeyStore {
        MemoryKeyStore {
            keys: HashMap::new(),
        }
    }

    /// Inserts a key-value pair into the KeyStore.
    ///
    /// If the store did not have this key present, None is returned.
    ///
    /// If the store did have this key (by Identity and Key_name) present, the value (Key) is updated and the old value is returned.
    pub fn insert(
        &mut self,
        identity: &str,
        key_name: &str,
        key_value: InMemoryKey,
    ) -> Option<InMemoryKey> {
        self.keys
            .insert(format!("{}{}", identity, key_name), key_value)
    }
}

impl KeyStore for MemoryKeyStore {
    type Key = InMemoryKey;

    fn get(&self, identity: &str, key_name: &str) -> Option<&Self::Key> {
        self.keys.get(&format!("{}{}", identity, key_name))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bytes::Bytes;

    #[test]
    fn sha256_sign_test_positive() {
        //Arrange
        let in_memory_key = InMemoryKey {
            key: Bytes::from("key"),
        };
        let data = b"The quick brown fox jumps over the lazy dog";
        let signature_algorithm = SignatureAlgorithm::HMACSHA256;
        //Act
        let result_hmac256 = in_memory_key.sign(signature_algorithm, data).unwrap();

        //Assert
        let expected_bytes = [
            0xf7, 0xbc, 0x83, 0xf4, 0x30, 0x53, 0x84, 0x24, 0xb1, 0x32, 0x98, 0xe6, 0xaa, 0x6f,
            0xb1, 0x43, 0xef, 0x4d, 0x59, 0xa1, 0x49, 0x46, 0x17, 0x59, 0x97, 0x47, 0x9d, 0xbc,
            0x2d, 0x1a, 0x3c, 0xd8,
        ];
        let expected_signature = Signature::new(Bytes::from(expected_bytes.as_ref()));

        assert_eq!(expected_bytes, result_hmac256.as_bytes());
        assert_eq!(expected_signature, result_hmac256);
    }

    #[test]
    fn sha256_sign_test_data_not_matching_shall_fail() {
        //Arrange
        let in_memory_key = InMemoryKey {
            key: Bytes::from("key"),
        };
        let data = b"The quick brown fox jumps over the lazy do";
        let signature_algorithm = SignatureAlgorithm::HMACSHA256;
        //Act
        let result_hmac256 = in_memory_key.sign(signature_algorithm, data).unwrap();

        //Assert
        let expected_bytes = [
            0xf7, 0xbc, 0x83, 0xf4, 0x30, 0x53, 0x84, 0x24, 0xb1, 0x32, 0x98, 0xe6, 0xaa, 0x6f,
            0xb1, 0x43, 0xef, 0x4d, 0x59, 0xa1, 0x49, 0x46, 0x17, 0x59, 0x97, 0x47, 0x9d, 0xbc,
            0x2d, 0x1a, 0x3c, 0xd8,
        ];

        let expected_signature = Signature::new(Bytes::from(expected_bytes.as_ref()));

        assert_ne!(expected_signature, result_hmac256);
    }

    #[test]
    fn sha256_sign_test_key_not_mathing_shall_fail() {
        //Arrange
        let in_memory_key = InMemoryKey {
            key: Bytes::from("wrongkey"),
        };
        let data = b"The quick brown fox jumps over the lazy dog";
        let signature_algorithm = SignatureAlgorithm::HMACSHA256;
        //Act
        let result_hmac256 = in_memory_key.sign(signature_algorithm, data).unwrap();

        //Assert
        let expected = [
            0xf7, 0xbc, 0x83, 0xf4, 0x30, 0x53, 0x84, 0x24, 0xb1, 0x32, 0x98, 0xe6, 0xaa, 0x6f,
            0xb1, 0x43, 0xef, 0x4d, 0x59, 0xa1, 0x49, 0x46, 0x17, 0x59, 0x97, 0x47, 0x9d, 0xbc,
            0x2d, 0x1a, 0x3c, 0xd8,
        ];

        assert_ne!(expected, result_hmac256.as_bytes());
    }

    //MemoryKeyStoreTests
    #[test]
    fn create_empty_memory_keystore() {
        //Arrange
        //Act
        let memoryKeyStore = MemoryKeyStore::new();

        //Assert
        assert_eq!(true, memoryKeyStore.keys.is_empty());
    }

    #[test]
    fn create_memory_keystore_1key() {
        //Arrange
        let mut memoryKeyStore = MemoryKeyStore::new();
        let in_memory_key = InMemoryKey {
            key: Bytes::from("anykey"),
        };

        //Act
        memoryKeyStore.insert("mod1", "key1", in_memory_key);

        //Assert
        assert_eq!(false, memoryKeyStore.keys.is_empty());
        assert_eq!(false, memoryKeyStore.get("mod1", "invalidKey").is_some());
        assert_eq!(true, memoryKeyStore.get("mod1", "key1").is_some());
    }

    #[test]
    fn create_memory_keystore_2keys() {
        //Arrange
        let mut memoryKeyStore = MemoryKeyStore::new();
        let in_memory_key = InMemoryKey {
            key: Bytes::from("anykey"),
        };

        let in_memory_key2 = InMemoryKey {
            key: Bytes::from("anykey"),
        };

        //Act
        memoryKeyStore.insert("mod1", "key1", in_memory_key);
        memoryKeyStore.insert("mod2", "key2", in_memory_key2);

        //Assert
        assert_eq!(false, memoryKeyStore.keys.is_empty());
        assert_eq!(false, memoryKeyStore.get("mod1", "invalidKey").is_some());
        assert_eq!(true, memoryKeyStore.get("mod1", "key1").is_some());
        assert_eq!(true, memoryKeyStore.get("mod2", "key2").is_some());
        assert_eq!(2, memoryKeyStore.keys.len());
    }
}
