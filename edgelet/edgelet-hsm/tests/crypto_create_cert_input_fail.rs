// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use lazy_static::lazy_static;
use std::sync::Mutex;

use edgelet_core::{
    CertificateIssuer, CertificateProperties, CertificateType, CreateCertificate, IOTEDGED_CA_ALIAS,
};
use edgelet_hsm::{Crypto, HsmLock};
mod test_utils;
use test_utils::TestHSMEnvSetup;

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

#[test]
fn crypto_create_cert_input_fail() {
    // arrange
    let _setup_home_dir = TestHSMEnvSetup::new(&LOCK, None);

    let hsm_lock = HsmLock::new();
    let crypto = Crypto::new(hsm_lock, 1000).unwrap();

    let edgelet_ca_props = CertificateProperties::new(
        3600,
        "test-iotedge-cn".to_string(),
        CertificateType::Ca,
        IOTEDGED_CA_ALIAS.to_string(),
    )
    .with_issuer(CertificateIssuer::DeviceCa);

    let _workload_ca_cert = crypto.create_certificate(&edgelet_ca_props).unwrap();

    let props_time = CertificateProperties::new(
        0,
        "Common Name".to_string(),
        CertificateType::Ca,
        "Alias".to_string(),
    );
    let props_cn = CertificateProperties::new(
        3600,
        "".to_string(),
        CertificateType::Ca,
        "Alias".to_string(),
    );
    let props_type = CertificateProperties::new(
        3600,
        "Common Name".to_string(),
        CertificateType::Unknown,
        "Alias".to_string(),
    );
    let props_a = CertificateProperties::new(
        3600,
        "Common Name".to_string(),
        CertificateType::Ca,
        "".to_string(),
    );

    // act
    crypto
        .create_certificate(&props_time)
        .expect_err("Expected an error from bad time");

    crypto
        .create_certificate(&props_cn)
        .expect_err("Expected an error from bad common name");

    crypto
        .create_certificate(&props_type)
        .expect_err("Expected an error from bad cert type");

    crypto
        .create_certificate(&props_a)
        .expect_err("Expected an error from bad alias");

    crypto
        .destroy_certificate("unknown_cert_alias".to_string())
        .expect("Expected no error when destroying a certificate that does not exist");

    // cleanup
    crypto
        .destroy_certificate(IOTEDGED_CA_ALIAS.to_string())
        .unwrap();
}
