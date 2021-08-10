// Copyright (c) Microsoft. All rights reserved.

pub struct KeyClient {}

impl Default for KeyClient {
    fn default() -> Self {
        KeyClient {}
    }
}

impl KeyClient {
    pub async fn create_key_if_not_exists(
        &self,
        id: &str,
        value: aziot_key_common::CreateKeyValue,
        usage: &[aziot_key_common::KeyUsage],
    ) -> std::io::Result<aziot_key_common::KeyHandle> {
        todo!()
    }

    pub async fn create_key_pair_if_not_exists(
        &self,
        id: &str,
        preferred_algorithms: Option<&str>,
    ) -> std::io::Result<aziot_key_common::KeyHandle> {
        todo!()
    }

    pub async fn encrypt(
        &self,
        handle: &aziot_key_common::KeyHandle,
        mechanism: aziot_key_common::EncryptMechanism,
        plaintext: &[u8],
    ) -> std::io::Result<Vec<u8>> {
        todo!()
    }

    pub async fn decrypt(
        &self,
        handle: &aziot_key_common::KeyHandle,
        mechanism: aziot_key_common::EncryptMechanism,
        ciphertext: &[u8],
    ) -> std::io::Result<Vec<u8>> {
        todo!()
    }

    pub async fn sign(
        &self,
        handle: &aziot_key_common::KeyHandle,
        mechanism: aziot_key_common::SignMechanism,
        digest: &[u8],
    ) -> std::io::Result<Vec<u8>> {
        todo!()
    }
}
