// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

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

    crypto
        .create_key()
        .expect("Create master key function returned error");

    //act
    let ciphertext = crypto
        .encrypt(client_id, plaintext, iv)
        .expect("Encrypt function returned error");
    assert_ne!(ciphertext.as_ref().len(), 0);

    //act
    let plaintext_result = crypto
        .decrypt(client_id, ciphertext.as_ref(), iv)
        .expect("Decrypt function returned error");
    assert_eq!(
        plaintext,
        plaintext_result.as_ref(),
        "Failure plaintext after decrypt did not match {:?} and {:?}",
        plaintext,
        plaintext_result.as_ref()
    );

    let bad_client_id = b"module2";
    crypto
        .decrypt(bad_client_id, ciphertext.as_ref(), iv)
        .expect_err("Decrypt function returned unexpected success");

    let bad_iv = b"inconsistent_iv";
    crypto
        .decrypt(client_id, ciphertext.as_ref(), bad_iv)
        .expect_err("Decrypt function returned unexpected success");

    // cleanup
    crypto
        .destroy_key()
        .expect("Destroy master key function returned error");
}
