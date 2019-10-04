// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use lazy_static::lazy_static;
use std::sync::Mutex;

use edgelet_core::{Certificate, GetTrustBundle};
use edgelet_hsm::{Crypto, HsmLock};
mod test_utils;
use test_utils::TestHSMEnvSetup;

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

#[test]
fn crypto_get_trust_bundle() {
    // arrange
    let _setup_home_dir = TestHSMEnvSetup::new(&LOCK, None);

    let hsm_lock = HsmLock::new();
    let crypto = Crypto::new(hsm_lock, 1000).unwrap();

    // act
    let cert_info = crypto.get_trust_bundle().unwrap();

    let buffer = cert_info.pem().unwrap();

    if cert_info.get_private_key().unwrap().is_some() {
        panic!("do not expect to find a key");
    }

    // assert
    // assume cert_type is PEM(0)
    assert!(!buffer.as_bytes().is_empty());
}
