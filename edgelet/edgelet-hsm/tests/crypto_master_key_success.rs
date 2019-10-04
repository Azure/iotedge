// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use lazy_static::lazy_static;
use std::sync::Mutex;

use edgelet_core::crypto::MasterEncryptionKey;
use edgelet_hsm::{Crypto, HsmLock};
mod test_utils;
use test_utils::TestHSMEnvSetup;

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

/// Encryption master key tests
#[test]
fn crypto_master_key_success() {
    // arrange
    let _setup_home_dir = TestHSMEnvSetup::new(&LOCK, None);

    let hsm_lock = HsmLock::new();
    let crypto = Crypto::new(hsm_lock, 1000).unwrap();

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
