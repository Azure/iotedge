// Copyright (c) Microsoft. All rights reserved.
extern crate edgelet_core;
extern crate edgelet_hsm;

use edgelet_core::crypto::{Decrypt, Encrypt};
use edgelet_hsm::Crypto;

/// Encrypt/Decrypt not working yet, expect this to fail.
#[test]
fn crypto_encrypt_decypt_fail() {
    // arrange
    let crypto = Crypto::default();

    let client_id = b"buffer1";
    let plaintext = b"plaintext";
    let ciphertext = b"crypttext";
    let iv = b"initialization vector";

    //act
    match crypto.encrypt(client_id, plaintext, None, iv) {
        //assert
        Ok(_) => panic!("Encrypt function is not yet implemented, but got a good result"),
        Err(_) => (),
    };

    //act
    match crypto.decrypt(client_id, ciphertext, None, iv) {
        //assert
        Ok(_) => panic!("Decrypt function is not yet implemented, but got a good result"),
        Err(_) => (),
    };
}
