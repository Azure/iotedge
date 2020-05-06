// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use lazy_static::lazy_static;
use std::sync::Mutex;

use edgelet_core::MakeRandom;
use edgelet_hsm::{Crypto, HsmLock};
mod test_utils;
use test_utils::TestHSMEnvSetup;

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

#[test]
fn crypto_random_bytes() {
    // arrange
    let _setup_home_dir = TestHSMEnvSetup::new(&LOCK, None);

    let hsm_lock = HsmLock::new();
    let crypto = Crypto::new(hsm_lock, 1000).unwrap();

    // act
    let smz: [u8; 16] = [0; 16];
    let mut sm1: [u8; 16] = [0; 16];
    let mut sm2: [u8; 16] = [0; 16];
    crypto.get_random_bytes(&mut sm1).unwrap();
    crypto.get_random_bytes(&mut sm2).unwrap();
    assert_ne!(smz, sm2);
    assert_ne!(sm1, sm2);

    let medz: [u8; 256] = [0; 256];
    let mut med: [u8; 256] = [0; 256];
    crypto.get_random_bytes(&mut med).unwrap();
    assert!(!medz.iter().eq(med.iter()));

    let lgz: [u8; 1024] = [0; 1024];
    let mut lg: [u8; 1024] = [0; 1024];
    crypto.get_random_bytes(&mut lg).unwrap();
    assert!(!lgz.iter().eq(lg.iter()));

    let xlz: [u8; 4096] = [0; 4096];
    let mut xl: [u8; 4096] = [0; 4096];
    crypto.get_random_bytes(&mut xl).unwrap();
    assert!(!xlz.iter().eq(xl.iter()));
}
