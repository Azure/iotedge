// Copyright (c) Microsoft. All rights reserved.

pub struct KeyClient {
    pub create_key_if_not_exists_ret: Option<aziot_key_common::KeyHandle>,
    pub create_key_pair_if_not_exists_ret: Option<aziot_key_common::KeyHandle>,

    pub encrypt_ret: Option<Vec<u8>>,
    pub decrypt_ret: Option<Vec<u8>>,
    pub sign_ret: Option<Vec<u8>>,
}

impl Default for KeyClient {
    fn default() -> Self {
        KeyClient {
            create_key_if_not_exists_ret: Some(aziot_key_common::KeyHandle(
                "key-handle".to_string(),
            )),
            create_key_pair_if_not_exists_ret: Some(aziot_key_common::KeyHandle(
                "key-pair-handle".to_string(),
            )),

            encrypt_ret: Some("ciphertext".as_bytes().to_owned()),
            decrypt_ret: Some("plaintext".as_bytes().to_owned()),
            sign_ret: Some("digest".as_bytes().to_owned()),
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
        match &self.create_key_if_not_exists_ret {
            Some(key_handle) => Ok(key_handle.clone()),
            None => Err(crate::test_error()),
        }
    }

    pub async fn create_key_pair_if_not_exists(
        &self,
        _id: &str,
        _preferred_algorithms: Option<&str>,
    ) -> std::io::Result<aziot_key_common::KeyHandle> {
        match &self.create_key_pair_if_not_exists_ret {
            Some(key_handle) => Ok(key_handle.clone()),
            None => Err(crate::test_error()),
        }
    }

    pub async fn encrypt(
        &self,
        _handle: &aziot_key_common::KeyHandle,
        _mechanism: aziot_key_common::EncryptMechanism,
        _plaintext: &[u8],
    ) -> std::io::Result<Vec<u8>> {
        match &self.encrypt_ret {
            Some(ciphertext) => Ok(ciphertext.clone()),
            None => Err(crate::test_error()),
        }
    }

    pub async fn decrypt(
        &self,
        _handle: &aziot_key_common::KeyHandle,
        _mechanism: aziot_key_common::EncryptMechanism,
        _ciphertext: &[u8],
    ) -> std::io::Result<Vec<u8>> {
        match &self.decrypt_ret {
            Some(plaintext) => Ok(plaintext.clone()),
            None => Err(crate::test_error()),
        }
    }

    pub async fn sign(
        &self,
        _handle: &aziot_key_common::KeyHandle,
        _mechanism: aziot_key_common::SignMechanism,
        _digest: &[u8],
    ) -> std::io::Result<Vec<u8>> {
        match &self.sign_ret {
            Some(digest) => Ok(digest.clone()),
            None => Err(crate::test_error()),
        }
    }
}
