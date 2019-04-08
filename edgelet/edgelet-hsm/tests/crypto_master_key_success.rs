// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use std::env;
use tempfile::TempDir;

use edgelet_core::crypto::MasterEncryptionKey;
use edgelet_hsm::Crypto;

const HOMEDIR_KEY: &str = "IOTEDGE_HOMEDIR";

/// Encryption master key tests
#[test]
fn crypto_master_key_success() {
    // arrange
    let home_dir = TempDir::new().unwrap();
    env::set_var(HOMEDIR_KEY, &home_dir.path());
    println!("IOTEDGE_HOMEDIR set to {:#?}", home_dir.path());

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

    home_dir.close().unwrap();
}
