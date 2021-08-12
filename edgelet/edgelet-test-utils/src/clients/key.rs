// Copyright (c) Microsoft. All rights reserved.

pub struct KeyClient {
    pub create_key_if_not_exists_ok: bool,
    pub create_key_pair_if_not_exists_ok: bool,

    pub encrypt_ok: bool,
    pub decrypt_ok: bool,
    pub sign_ok: bool,
}

impl Default for KeyClient {
    fn default() -> Self {
        KeyClient {
            create_key_if_not_exists_ok: true,
            create_key_pair_if_not_exists_ok: true,
            encrypt_ok: true,
            decrypt_ok: true,
            sign_ok: true,
        }
    }
}

impl KeyClient {
    pub async fn create_key_if_not_exists(
        &self,
        _id: &str,
        _value: aziot_key_common::CreateKeyValue,
        _usage: &[aziot_key_common::KeyUsage],
    ) -> std::io::Result<aziot_key_common::KeyHandle> {
        if self.create_key_if_not_exists_ok {
            Ok(aziot_key_common::KeyHandle("key-handle".to_string()))
        } else {
            Err(crate::test_error())
        }
    }

    pub async fn create_key_pair_if_not_exists(
        &self,
        _id: &str,
        _preferred_algorithms: Option<&str>,
    ) -> std::io::Result<aziot_key_common::KeyHandle> {
        if self.create_key_pair_if_not_exists_ok {
            Ok(aziot_key_common::KeyHandle("key-pair-handle".to_string()))
        } else {
            Err(crate::test_error())
        }
    }

    pub async fn encrypt(
        &self,
        _handle: &aziot_key_common::KeyHandle,
        _mechanism: aziot_key_common::EncryptMechanism,
        _plaintext: &[u8],
    ) -> std::io::Result<Vec<u8>> {
        if self.encrypt_ok {
            Ok("ciphertext".as_bytes().to_owned())
        } else {
            Err(crate::test_error())
        }
    }

    pub async fn decrypt(
        &self,
        _handle: &aziot_key_common::KeyHandle,
        _mechanism: aziot_key_common::EncryptMechanism,
        _ciphertext: &[u8],
    ) -> std::io::Result<Vec<u8>> {
        if self.decrypt_ok {
            Ok("plaintext".as_bytes().to_owned())
        } else {
            Err(crate::test_error())
        }
    }

    pub async fn sign(
        &self,
        _handle: &aziot_key_common::KeyHandle,
        _mechanism: aziot_key_common::SignMechanism,
        _digest: &[u8],
    ) -> std::io::Result<Vec<u8>> {
        if self.sign_ok {
            Ok("digest".as_bytes().to_owned())
        } else {
            Err(crate::test_error())
        }
    }
}

pub struct KeyEngine {
    pub keys: std::collections::BTreeMap<
        String,
        (
            openssl::pkey::PKey<openssl::pkey::Private>,
            openssl::pkey::PKey<openssl::pkey::Public>,
        ),
    >,
}

impl KeyEngine {
    pub fn load(_client: std::sync::Arc<aziot_key_client::Client>) -> Result<Self, std::io::Error> {
        Ok(KeyEngine {
            keys: std::collections::BTreeMap::new(),
        })
    }

    pub fn load_private_key(
        &mut self,
        key_handle: &std::ffi::CString,
    ) -> Result<openssl::pkey::PKey<openssl::pkey::Private>, std::io::Error> {
        let key_handle = key_handle.to_str().unwrap();

        match self.keys.get(key_handle) {
            Some((private_key, _)) => Ok(private_key.clone()),
            None => Ok(self.create_keys(key_handle).0),
        }
    }

    pub fn load_public_key(
        &mut self,
        key_handle: &std::ffi::CString,
    ) -> Result<openssl::pkey::PKey<openssl::pkey::Public>, std::io::Error> {
        let key_handle = key_handle.to_str().unwrap();

        match self.keys.get(key_handle) {
            Some((_, public_key)) => Ok(public_key.clone()),
            None => Ok(self.create_keys(key_handle).1),
        }
    }

    fn create_keys(
        &mut self,
        key_handle: &str,
    ) -> (
        openssl::pkey::PKey<openssl::pkey::Private>,
        openssl::pkey::PKey<openssl::pkey::Public>,
    ) {
        let rsa = openssl::rsa::Rsa::generate(2048).unwrap();
        let private_key = openssl::pkey::PKey::from_rsa(rsa).unwrap();

        let public_key = private_key.public_key_to_pem().unwrap();
        let public_key = openssl::pkey::PKey::public_key_from_pem(&public_key).unwrap();

        assert!(self
            .keys
            .insert(
                key_handle.to_string(),
                (private_key.clone(), public_key.clone())
            )
            .is_none());

        (private_key, public_key)
    }
}
