// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use std::str;
use std::sync::Mutex;

use bytes::Bytes;
use lazy_static::lazy_static;

use edgelet_core::crypto::Activate;
use edgelet_core::crypto::Sign;
use edgelet_core::crypto::SignatureAlgorithm;
use edgelet_core::KeyIdentity;
use edgelet_core::KeyStore;
use edgelet_hsm::TpmKeyStore;
mod test_utils;
use test_utils::TestHSMEnvSetup;

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

const TEST_KEY_BASE64: &str = "D7PuplFy7vIr0349blOugqCxyfMscyVZDoV9Ii0EFnA=";

// The HSM implementation expects keys, identity and data to be non-zero length.
#[test]
fn tpm_input_tests() {
    // arrange
    let _setup_home_dir = TestHSMEnvSetup::new(&LOCK, None);

    let mut key_store = TpmKeyStore::new().unwrap();

    let decoded_key = base64::decode(TEST_KEY_BASE64).unwrap();
    let decoded_key_str = unsafe { str::from_utf8_unchecked(&decoded_key) };
    let module1_str: &str = "module1";
    let module1_identity: KeyIdentity = KeyIdentity::Module(module1_str.to_string());

    key_store
        .activate_identity_key(
            module1_identity.clone(),
            "ignored".to_string(),
            &Bytes::from(decoded_key_str),
        )
        .expect_err("Module key cannot be activated");

    key_store
        .activate_identity_key(KeyIdentity::Device, "ignored".to_string(), &Bytes::from(""))
        .expect_err("empty key is not allowed");

    key_store
        .activate_key(&Bytes::from(""))
        .expect_err("empty key is not allowed");

    key_store
        .activate_key(&Bytes::from(decoded_key_str))
        .unwrap();

    key_store
        .get(&KeyIdentity::Device, "ignored")
        .expect("could not get device key");

    key_store
        .get(&KeyIdentity::Module("".to_string()), "ignored")
        .expect_err("empty identity is not allowed");

    key_store
        .get(&module1_identity, "")
        .expect_err("empty Key name is not allowed");

    let empty_data = b"";

    let active_key = key_store.get_active_key().unwrap();

    active_key
        .sign(SignatureAlgorithm::HMACSHA256, empty_data)
        .expect_err("empty data is not allowed");

    let key_with_id = key_store.get(&module1_identity, "ignored").unwrap();

    key_with_id
        .sign(SignatureAlgorithm::HMACSHA256, empty_data)
        .expect_err("empty data is not allowed");
}
