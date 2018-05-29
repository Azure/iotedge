// Copyright (c) Microsoft. All rights reserved.

extern crate base64;
extern crate bytes;
extern crate edgelet_core;
extern crate edgelet_hsm;

use std::str;

use bytes::Bytes;
use edgelet_core::crypto::Sign;
use edgelet_core::crypto::SignatureAlgorithm;
use edgelet_core::KeyStore;
use edgelet_hsm::TpmKeyStore;

const TEST_KEY_BASE64: &'static str = "D7PuplFy7vIr0349blOugqCxyfMscyVZDoV9Ii0EFnA=";

// The HSM implementation expects keys, identity and data to be non-zero length.
#[test]
fn tpm_input_tests() {
    let key_store = TpmKeyStore::new().unwrap();

    let decoded_key = base64::decode(TEST_KEY_BASE64).unwrap();
    let decoded_key_str = unsafe { str::from_utf8_unchecked(&decoded_key) };
    let module1_identity: &str = "module1";

    match key_store.activate_key(&Bytes::from("")) {
        Ok(()) => panic!("empty key is not allowed"),
        Err(_) => (),
    };

    key_store
        .activate_key(&Bytes::from(decoded_key_str))
        .unwrap();

    match key_store.get("", "ignored") {
        Ok(_) => panic!("empty identity is not allowed"),
        Err(_) => (),
    };

    match key_store.get(module1_identity, "") {
        Ok(_) => (),
        Err(_) => panic!("Key name is unused for Tpm KeyStore"),
    };

    let empty_data = b"";

    let active_key = key_store.get_active_key().unwrap();

    match active_key.sign(SignatureAlgorithm::HMACSHA256, empty_data) {
        Ok(_) => panic!("empty data is not allowed"),
        Err(_) => (),
    };

    let key_with_id = key_store.get(module1_identity, "ignored").unwrap();

    match key_with_id.sign(SignatureAlgorithm::HMACSHA256, empty_data) {
        Ok(_) => panic!("empty data is not allowed"),
        Err(_) => (),
    };
}
