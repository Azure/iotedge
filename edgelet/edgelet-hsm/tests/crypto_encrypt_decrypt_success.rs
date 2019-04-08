// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use std::env;
use tempfile::TempDir;

use edgelet_core::crypto::{Decrypt, Encrypt, MasterEncryptionKey};
use edgelet_hsm::Crypto;

const HOMEDIR_KEY: &str = "IOTEDGE_HOMEDIR";

/// Encrypt/Decrypt tests
#[test]
fn crypto_encrypt_decypt_success() {
    // arrange
    let home_dir = TempDir::new().unwrap();
    env::set_var(HOMEDIR_KEY, &home_dir.path());
    println!("IOTEDGE_HOMEDIR set to {:#?}", home_dir.path());

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

    home_dir.close().unwrap();
}
