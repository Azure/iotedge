// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::must_use_candidate)]

use std::str;
use std::sync::Mutex;

use hmac::{Hmac, Mac};
use lazy_static::lazy_static;
use sha2::Sha256;

use bytes::Bytes;
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

pub fn test_helper_compute_hmac(key: &[u8], input: &[u8]) -> Vec<u8> {
    let mut mac = Hmac::<Sha256>::new(key).unwrap();
    mac.input(input);
    mac.result().code().as_slice().to_vec()
}

// This tests the following:
//  1) A well known identity key K can be installed in the TPM
//  2) For a specific derived identity IDderived a HMACSHA256 digest sign request
//     should return a digest whose value would be obtained
//     by performing the following computations:
//     Kderived = HMACSHA256(K, IDderived)
//     digest   = HMACSHA256(Kderived, DATA)
//  3) The HMACSHA256 digest sign request for different data produces a different output.
//
#[test]
fn tpm_basic_test() {
    // arrange
    let _setup_home_dir = TestHSMEnvSetup::new(&LOCK, None);

    let hsm_lock = HsmLock::new();
    let key_store = TpmKeyStore::new(hsm_lock).unwrap();

    let decoded_key = base64::decode(TEST_KEY_BASE64).unwrap();
    let decoded_key_str = unsafe { str::from_utf8_unchecked(&decoded_key) };
    let module1_str = "module1";
    let module1_identity: KeyIdentity = KeyIdentity::Module(module1_str.to_string());

    key_store
        .activate_key(&Bytes::from(decoded_key_str))
        .unwrap();

    let key1 = key_store.get(&module1_identity, "primary").unwrap();

    let data_to_be_signed1 = b"I am the very model of a modern major general";
    let data_to_be_signed2 = b"I've information vegetable, animal, and mineral,";

    // compute expected result
    let test_expected_primary_key_buf = test_helper_compute_hmac(
        decoded_key_str.as_bytes(),
        format!("{}{}", module1_str, "primary").as_bytes(),
    );

    let test_expected_digest1 =
        test_helper_compute_hmac(test_expected_primary_key_buf.as_slice(), data_to_be_signed1);
    let test_expected_digest2 =
        test_helper_compute_hmac(test_expected_primary_key_buf.as_slice(), data_to_be_signed2);

    // act
    let digest1 = key1
        .sign(SignatureAlgorithm::HMACSHA256, data_to_be_signed1)
        .unwrap();
    let digest2 = key1
        .sign(SignatureAlgorithm::HMACSHA256, data_to_be_signed2)
        .unwrap();

    // assert
    assert_eq!(test_expected_digest1.as_slice(), digest1.as_bytes());
    assert_eq!(test_expected_digest2.as_slice(), digest2.as_bytes());
    assert_ne!(digest1.as_bytes(), digest2.as_bytes());
}
