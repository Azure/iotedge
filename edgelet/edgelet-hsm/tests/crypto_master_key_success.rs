// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]

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
