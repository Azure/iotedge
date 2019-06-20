// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use std::str;
use std::sync::Mutex;

use bytes::Bytes;
use lazy_static::lazy_static;

use edgelet_core::crypto::Sign;
use edgelet_core::crypto::Signature;
use edgelet_core::crypto::SignatureAlgorithm;
use edgelet_core::KeyIdentity;
use edgelet_core::KeyStore;
use edgelet_hsm::{HsmLock, TpmKeyStore};
mod test_utils;
use test_utils::TestHSMEnvSetup;

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

const TEST_KEY_BASE64: &str = "D7PuplFy7vIr0349blOugqCxyfMscyVZDoV9Ii0EFnA=";

// This tests the following:
//  1) A well known identity key K can be installed in the TPM
//  2) The HMACSHA256 digest sign request for a known payload DATA
//     should return a digest whose value would be diferent based on the identity.
#[test]
fn tpm_identity_affects_digest() {
    // arrange
    let _setup_home_dir = TestHSMEnvSetup::new(&LOCK, None);

    let hsm_lock = HsmLock::new();
    let key_store = TpmKeyStore::new(hsm_lock).unwrap();

    let decoded_key = base64::decode(TEST_KEY_BASE64).unwrap();
    let decoded_key_str = unsafe { str::from_utf8_unchecked(&decoded_key) };
    let module1_identity: KeyIdentity = KeyIdentity::Module("module1".to_string());
    let module2_identity: KeyIdentity = KeyIdentity::Module("module2".to_string());

    key_store
        .activate_key(&Bytes::from(decoded_key_str))
        .unwrap();

    let key1 = key_store.get(&module1_identity, "fixed value").unwrap();
    let key2 = key_store.get(&module2_identity, "fixed value").unwrap();

    let data_to_be_signed = b"I am the very model of a modern major general";

    // act
    let digest1 = key1
        .sign(SignatureAlgorithm::HMACSHA256, data_to_be_signed)
        .unwrap();
    let digest2 = key2
        .sign(SignatureAlgorithm::HMACSHA256, data_to_be_signed)
        .unwrap();

    // assert
    assert_ne!(digest1.as_bytes(), digest2.as_bytes());
}
