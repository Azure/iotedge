// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use lazy_static::lazy_static;
use std::env;
use std::sync::Mutex;

use edgelet_core::{Certificate, GetDeviceIdentityCertificate, KeyBytes, PrivateKey, Signature};
use edgelet_hsm::{HsmLock, X509};
mod test_utils;
use test_utils::TestHSMEnvSetup;

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

const REGISTRATION_ID_KEY: &str = "IOTEDGE_REGISTRATION_ID";

#[test]
fn x509_get_gen_identity_cert_success() {
    // arrange
    let _setup_home_dir = TestHSMEnvSetup::new(&LOCK, None);

    env::set_var(REGISTRATION_ID_KEY, "TEST X509 DEVICE");

    let hsm_lock = HsmLock::new();
    let x509 = X509::new(hsm_lock).unwrap();

    let cert_info = x509.get().unwrap();

    assert!(cert_info.get_valid_to().is_ok());

    let buffer = cert_info.pem().unwrap();
    assert!(!buffer.as_bytes().is_empty());

    let pk = match cert_info.get_private_key().unwrap() {
        Some(pk) => pk,
        None => panic!("Expected to find a key"),
    };

    match pk {
        PrivateKey::Ref(_) => panic!("did not expect reference private key"),
        PrivateKey::Key(KeyBytes::Pem(k)) => assert!(!k.as_bytes().is_empty()),
    }

    let buffer = x509.sign_with_private_key(b"sign me up").unwrap();
    assert!(!buffer.as_bytes().is_empty());

    // cleanup
    env::remove_var(REGISTRATION_ID_KEY);
}
