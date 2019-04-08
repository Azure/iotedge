// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use std::env;
use tempfile::TempDir;

use edgelet_core::{Certificate, GetDeviceIdentityCertificate, KeyBytes, PrivateKey, Signature};
use edgelet_hsm::X509;

const HOMEDIR_KEY: &str = "IOTEDGE_HOMEDIR";
const REGISTRATION_ID_KEY: &str = "IOTEDGE_REGISTRATION_ID";

#[test]
fn x509_get_identity_cert_success() {
    // arrange
    let home_dir = TempDir::new().unwrap();
    env::set_var(HOMEDIR_KEY, &home_dir.path());
    println!("IOTEDGE_HOMEDIR set to {:#?}", home_dir.path());
    env::set_var(REGISTRATION_ID_KEY, "TEST X509 DEVICE");

    let x509 = X509::new().unwrap();

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

    // cleanup
    home_dir.close().unwrap();
}
