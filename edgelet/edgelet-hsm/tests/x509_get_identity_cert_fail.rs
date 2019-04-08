// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use std::env;
use tempfile::TempDir;

use edgelet_core::{GetDeviceIdentityCertificate};
use edgelet_hsm::X509;

const HOMEDIR_KEY: &str = "IOTEDGE_HOMEDIR";

#[test]
fn x509_get_identity_cert_fails() {
    // arrange
    let home_dir = TempDir::new().unwrap();
    env::set_var(HOMEDIR_KEY, &home_dir.path());
    println!("IOTEDGE_HOMEDIR set to {:#?}", home_dir.path());

    let x509 = X509::new().unwrap();

    let cert_info = x509.get();

    assert!(cert_info.is_err());

    // cleanup
    home_dir.close().unwrap();
}
