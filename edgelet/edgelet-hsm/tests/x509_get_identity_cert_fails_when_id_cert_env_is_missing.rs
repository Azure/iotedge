// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use std::env;
use std::fs::File;
use std::io::Write;
use std::path::Path;
use std::sync::Mutex;

use lazy_static::lazy_static;

use edgelet_hsm::{HsmLock, X509};
mod test_utils;
use test_utils::TestHSMEnvSetup;

const DEVICE_IDENTITY_PK_KEY: &str = "IOTEDGE_DEVICE_IDENTITY_PK";
const DEVICE_IDENTITY_PK: &str = "ABCD";

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

fn setup_configured_id_cert(home_dir: &Path) {
    let file_path = home_dir.join("temp_key.pem");
    let mut key_file = File::create(file_path.clone()).unwrap();
    write!(key_file, "{}", DEVICE_IDENTITY_PK).unwrap();
    env::set_var(DEVICE_IDENTITY_PK_KEY, file_path);
    key_file.sync_all().unwrap();
}

#[test]
fn x509_get_conf_x509_identity_missing_cert_env_fails() {
    // arrange
    let home_dir = TestHSMEnvSetup::new(&LOCK, None);
    setup_configured_id_cert(home_dir.get_path());

    let hsm_lock = HsmLock::new();
    assert!(X509::new(hsm_lock, 1000).is_err());
}
