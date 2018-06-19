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
        Ok(_result) => assert!(true),
        Err(_) => panic!("Destroy master key returned error"),
    };

    match crypto.create_key() {
        // assert
        Ok(_result) => assert!(true),
        Err(_) => panic!("First create master key function returned error"),
    };

    match crypto.create_key() {
        // assert
        Ok(_result) => assert!(true),
        Err(_) => panic!("Second master key function returned error"),
    };

    match crypto.destroy_key() {
        // assert
        Ok(_result) => assert!(true),
        Err(_) => panic!("First destroy master key function returned error"),
    };

    match crypto.destroy_key() {
        // assert
        Ok(_result) => assert!(true),
        Err(_) => panic!("Second destroy master key function returned error"),
    };
}
