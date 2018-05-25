// Copyright (c) Microsoft. All rights reserved.
extern crate edgelet_core;
extern crate edgelet_hsm;

use edgelet_core::crypto::{Decrypt, Encrypt};
use edgelet_hsm::Crypto;

/// Encrypt/Decrypt tests
#[test]
fn crypto_encrypt_decypt_success() {
    // arrange
    let crypto = Crypto::default();

    let client_id = b"buffer1";
    let plaintext = b"plaintext";
    let ciphertext = b"crypttext";
    let iv = b"initialization vector";

    //act
    match crypto.encrypt(client_id, plaintext, iv) {
        //assert
        Ok(result) => assert_ne!(result.as_ref().len(), 0),
        Err(_) => panic!("Encrypt function returned error"),
    };

    //act
    match crypto.decrypt(client_id, ciphertext, iv) {
        //assert
        Ok(result) => assert_ne!(result.as_ref().len(), 0),
        Err(_) => panic!("Decrypt function returned error"),
    };
}
