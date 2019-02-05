// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

extern crate base64;
extern crate bytes;
extern crate edgelet_core;
extern crate edgelet_hsm;
extern crate hmac;
extern crate sha2;

use std::str;

use bytes::Bytes;
use hmac::{Hmac, Mac};
use sha2::Sha256;

use edgelet_core::crypto::Sign;
use edgelet_core::crypto::Signature;
use edgelet_core::crypto::SignatureAlgorithm;
use edgelet_hsm::TpmKeyStore;

const TEST_KEY_BASE64: &str = "D7PuplFy7vIr0349blOugqCxyfMscyVZDoV9Ii0EFnA=";

pub fn test_helper_compute_hmac(key: &[u8], input: &[u8]) -> Vec<u8> {
    let mut mac = Hmac::<Sha256>::new(key).unwrap();
    mac.input(input);
    mac.result().code().as_slice().to_vec()
}

// This tests the following:
//  1) A well known identity key K can be installed in the TPM
//  2) The HMACSHA256 digest sign request for a known payload DATA
//     should return a digest whose value would be the same as would be
//     expected by performing an actual HMACSHA256(K, DATA) computation.
//  3) The HMACSHA256 digest sign request for different data produces a different output.
//
#[test]
fn tpm_active_key_sign() {
    // arrange
    let key_store = TpmKeyStore::new().unwrap();

    let decoded_key = base64::decode(TEST_KEY_BASE64).unwrap();
    let decoded_key_str = unsafe { str::from_utf8_unchecked(&decoded_key) };

    key_store
        .activate_key(&Bytes::from(decoded_key_str))
        .unwrap();

    let key1 = key_store.get_active_key().unwrap();

    let data_to_be_signed1 = b"I am the very model of a modern major general";
    let data_to_be_signed2 = b"I've information vegetable, animal, and mineral,";

    // compute expected result
    let test_expected_digest1 =
        test_helper_compute_hmac(decoded_key_str.as_bytes(), data_to_be_signed1);
    let test_expected_digest2 =
        test_helper_compute_hmac(decoded_key_str.as_bytes(), data_to_be_signed2);

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
