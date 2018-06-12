// Copyright (c) Microsoft. All rights reserved.
extern crate edgelet_core;
extern crate edgelet_hsm;

use edgelet_core::crypto::MasterEncryptionKey;
use edgelet_hsm::Crypto;

/// Encryption master key tests
#[test]
fn crypto_master_key_success() {
    // arrange
    let crypto = Crypto::new().unwrap();

    // act
    match crypto.destroy_key() {
        // assert
        Ok(_result) => panic!("Destroy master key returned unexpected success"),
        Err(_) => assert!(true),
    };

    match crypto.create_key() {
        // assert
        Ok(_result) => assert!(true),
        Err(_) => panic!("Create master key function returned error"),
    };

    match crypto.destroy_key() {
        // assert
        Ok(_result) => assert!(true),
        Err(_) => panic!("Destroy master key function returned error"),
    };
}
