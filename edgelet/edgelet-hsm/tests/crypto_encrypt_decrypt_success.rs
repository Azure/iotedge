// Copyright (c) Microsoft. All rights reserved.
extern crate edgelet_core;
extern crate edgelet_hsm;

use edgelet_core::crypto::{Decrypt, Encrypt, MasterEncryptionKey};
use edgelet_hsm::Crypto;

/// Encrypt/Decrypt tests
#[test]
fn crypto_encrypt_decypt_success() {
    // arrange
    let crypto = Crypto::new().unwrap();

    let client_id = b"module1";
    let plaintext = b"plaintext";
    let iv = b"initialization vector";

    match crypto.create_key() {
        Ok(_result) => assert!(true),
        Err(_) => panic!("Create master key function returned error"),
    };

    //act
    let ciphertext = match crypto.encrypt(client_id, plaintext, iv) {
        //assert
        Ok(result) => result,
        Err(_) => panic!("Encrypt function returned error"),
    };
    assert_ne!(ciphertext.as_ref().len(), 0);

    //act
    let plaintext_result = match crypto.decrypt(client_id, ciphertext.as_ref(), iv) {
        //assert
        Ok(result) => result,
        Err(_) => panic!("Decrypt function returned error"),
    };
    assert_eq!(
        plaintext,
        plaintext_result.as_ref(),
        "Failure plaintext after decrypt did not match {:?} and {:?}",
        plaintext,
        plaintext_result.as_ref()
    );

    let bad_client_id = b"module2";
    match crypto.decrypt(bad_client_id, ciphertext.as_ref(), iv) {
        //assert
        Ok(_result) => panic!("Decrypt function returned unexpected success"),
        Err(_) => (),
    };

    let bad_iv = b"inconsistent_iv";
    match crypto.decrypt(client_id, ciphertext.as_ref(), bad_iv) {
        //assert
        Ok(_result) => panic!("Decrypt function returned unexpected success"),
        Err(_) => (),
    };

    // cleanup
    match crypto.destroy_key() {
        Ok(_result) => assert!(true),
        Err(_) => panic!("Destroy master key function returned error"),
    };
}
