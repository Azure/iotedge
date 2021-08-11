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
