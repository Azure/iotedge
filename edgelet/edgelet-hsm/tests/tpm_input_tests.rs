// Copyright (c) Microsoft. All rights reserved.

extern crate base64;
extern crate bytes;
extern crate edgelet_core;
extern crate edgelet_hsm;

use std::str;

use bytes::Bytes;
use edgelet_core::crypto::Activate;
use edgelet_core::crypto::Sign;
use edgelet_core::crypto::SignatureAlgorithm;
use edgelet_core::KeyIdentity;
use edgelet_core::KeyStore;
use edgelet_hsm::TpmKeyStore;

const TEST_KEY_BASE64: &'static str = "D7PuplFy7vIr0349blOugqCxyfMscyVZDoV9Ii0EFnA=";

// The HSM implementation expects keys, identity and data to be non-zero length.
#[test]
fn tpm_input_tests() {
    let mut key_store = TpmKeyStore::new().unwrap();

    let decoded_key = base64::decode(TEST_KEY_BASE64).unwrap();
    let decoded_key_str = unsafe { str::from_utf8_unchecked(&decoded_key) };
    let module1_str: &str = "module1";
    let module1_identity: KeyIdentity = KeyIdentity::Module(module1_str.to_string());

    match key_store.activate_identity_key(
        module1_identity.clone(),
        "ignored".to_string(),
        &Bytes::from(decoded_key_str),
    ) {
        Ok(()) => panic!("Module key cannot be activated"),
        Err(_) => (),
    };

    match key_store.activate_identity_key(
        KeyIdentity::Device,
        "ignored".to_string(),
        &Bytes::from(""),
    ) {
        Ok(()) => panic!("empty key is not allowed"),
        Err(_) => (),
    };

    match key_store.activate_key(&Bytes::from("")) {
        Ok(()) => panic!("empty key is not allowed"),
        Err(_) => (),
    };

    key_store
        .activate_key(&Bytes::from(decoded_key_str))
        .unwrap();

    match key_store.get(&KeyIdentity::Device, "ignored") {
        Ok(_) => (),
        Err(_) => panic!("could not get device key"),
    };

    match key_store.get(&KeyIdentity::Module("".to_string()), "ignored") {
        Ok(_) => panic!("empty identity is not allowed"),
        Err(_) => (),
    };

    match key_store.get(&module1_identity, "") {
        Ok(_) => panic!("empty Key name is not allowed"),
        Err(_) => (),
    };

    let empty_data = b"";

    let active_key = key_store.get_active_key().unwrap();

    match active_key.sign(SignatureAlgorithm::HMACSHA256, empty_data) {
        Ok(_) => panic!("empty data is not allowed"),
        Err(_) => (),
    };

    let key_with_id = key_store.get(&module1_identity, "ignored").unwrap();

    match key_with_id.sign(SignatureAlgorithm::HMACSHA256, empty_data) {
        Ok(_) => panic!("empty data is not allowed"),
        Err(_) => (),
    };
}
