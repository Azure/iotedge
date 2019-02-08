// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

extern crate edgelet_core;
extern crate edgelet_hsm;

use edgelet_core::crypto::MasterEncryptionKey;
use edgelet_hsm::Crypto;

/// Encryption master key tests
#[test]
fn crypto_master_key_success() {
    let crypto = Crypto::new().unwrap();

    crypto
        .destroy_key()
        .expect("Destroy master key returned error");

    crypto
        .create_key()
        .expect("First create master key function returned error");

    crypto
        .create_key()
        .expect("Second master key function returned error");

    crypto
        .destroy_key()
        .expect("First destroy master key function returned error");

    crypto
        .destroy_key()
        .expect("Second destroy master key function returned error");
}
