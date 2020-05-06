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

const DEVICE_IDENTITY_CERT_KEY: &str = "IOTEDGE_DEVICE_IDENTITY_CERT";
// the leaf device certificate created below was created using the instructions
// https://github.com/Azure/iotedge/tree/master/tools/CACertificates
// Essentially follow the instruction to create an IoT Leaf Device and simply
// copy the contents of the generated certificate here. For this test the key
// needn't be copied.
const DEVICE_IDENTITY_CERT: &str = "-----BEGIN CERTIFICATE-----\nMIICpDCCAYwCCQCgAJQdOd6dNzANBgkqhkiG9w0BAQsFADAUMRIwEAYDVQQDDAlsb2NhbGhvc3QwHhcNMTcwMTIwMTkyNTMzWhcNMjcwMTE4MTkyNTMzWjAUMRIwEAYDVQQDDAlsb2NhbGhvc3QwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDlJ3fRNWm05BRAhgUY7cpzaxHZIORomZaOp2Uua5yv+psdkpv35ExLhKGrUIK1AJLZylnue0ohZfKPFTnoxMHOecnaaXZ9RA25M7XGQvw85ePlGOZKKf3zXw3Ds58GFY6Sr1SqtDopcDuMmDSg/afYVvGHDjb2Fc4hZFip350AADcmjH5SfWuxgptCY2Jl6ImJoOpxt+imWsJCJEmwZaXw+eZBb87e/9PH4DMXjIUFZebShowAfTh/sinfwRkaLVQ7uJI82Ka/icm6Hmr56j7U81gDaF0DhC03ds5lhN7nMp5aqaKeEJiSGdiyyHAescfxLO/SMunNc/eG7iAirY7BAgMBAAEwDQYJKoZIhvcNAQELBQADggEBACU7TRogb8sEbv+SGzxKSgWKKbw+FNgC4Zi6Fz59t+4jORZkoZ8W87NM946wvkIpxbLKuc4F+7nTGHHksyHIiGC3qPpi4vWpqVeNAP+kfQptFoWEOzxD7jQTWIcqYhvssKZGwDk06c/WtvVnhZOZW+zzJKXA7mbwJrfp8VekOnN5zPwrOCumDiRX7BnEtMjqFDgdMgs9ohR5aFsI7tsqp+dToLKaZqBLTvYwCgCJCxdg3QvMhVD8OxcEIFJtDEwm3h9WFFO3ocabCmcMDyXUL354yaZ7RphCBLd06XXdaUU/eV6fOjY6T5ka4ZRJcYDJtjxSG04XPtxswQfrPGGoFhk=\n-----END CERTIFICATE-----\n";

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

fn setup_configured_id_cert(home_dir: &Path) {
    let file_path = home_dir.join("temp_cert.pem");
    let mut cert_file = File::create(file_path.clone()).unwrap();
    write!(cert_file, "{}", DEVICE_IDENTITY_CERT).unwrap();
    env::set_var(DEVICE_IDENTITY_CERT_KEY, file_path);
    cert_file.sync_all().unwrap();
}

#[test]
fn x509_get_conf_x509_identity_missing_pk_env_fails() {
    // arrange
    let home_dir = TestHSMEnvSetup::new(&LOCK, None);
    setup_configured_id_cert(home_dir.get_path());

    let hsm_lock = HsmLock::new();
    assert!(X509::new(hsm_lock, 1000).is_err());
}
