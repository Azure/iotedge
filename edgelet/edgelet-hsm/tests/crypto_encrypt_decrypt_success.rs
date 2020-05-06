// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use lazy_static::lazy_static;
use std::sync::Mutex;

use edgelet_core::crypto::{Decrypt, Encrypt, MasterEncryptionKey};
use edgelet_hsm::{Crypto, HsmLock};
mod test_utils;
use test_utils::TestHSMEnvSetup;

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

/// Encrypt/Decrypt tests
#[test]
fn crypto_encrypt_decypt_success() {
    // arrange
    let _setup_home_dir = TestHSMEnvSetup::new(&LOCK, None);

    let hsm_lock = HsmLock::new();
    let crypto = Crypto::new(hsm_lock, 1000).unwrap();

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
