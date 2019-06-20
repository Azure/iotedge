// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use lazy_static::lazy_static;
use std::convert::TryFrom;
use std::sync::Mutex;

use edgelet_core::{
    Certificate, CertificateIssuer, CertificateProperties, CertificateType, CreateCertificate,
    GetIssuerAlias, KeyBytes, PrivateKey, Signature, IOTEDGED_CA_ALIAS,
};
use edgelet_hsm::{Crypto, HsmLock};
mod test_utils;
use test_utils::TestHSMEnvSetup;

lazy_static! {
    static ref LOCK: Mutex<()> = Mutex::new(());
}

#[test]
fn crypto_create_cert_success() {
    // arrange
    let _setup_home_dir = TestHSMEnvSetup::new(&LOCK, None);

    let hsm_lock = HsmLock::new();
    let crypto = Crypto::new(hsm_lock).unwrap();

    // tests to ensure that the Device CA alias exists and is valid
    assert!(crypto
        .get_issuer_alias(CertificateIssuer::DefaultCa)
        .is_err());
    let issuer_alias = crypto
        .get_issuer_alias(CertificateIssuer::DeviceCa)
        .unwrap();
    assert!(!issuer_alias.is_empty());

    // ensure workload CA does not exist
    let workload_ca_cert = crypto.get_certificate(IOTEDGED_CA_ALIAS.to_string());
    assert!(workload_ca_cert.is_err());

    let issuer_ca = crypto.get_certificate(issuer_alias).unwrap();
    let issuer_validity = issuer_ca.get_valid_to().unwrap();

    let now = chrono::Utc::now();

    let diff = issuer_validity.timestamp() - now.timestamp();
    assert!(diff > 0);
    // create the default issuing CA cert properties
    let edgelet_ca_props = CertificateProperties::new(
        u64::try_from(diff).unwrap(),
        "test-iotedge-cn".to_string(),
        CertificateType::Ca,
        IOTEDGED_CA_ALIAS.to_string(),
    )
    .with_issuer(CertificateIssuer::DeviceCa);

    // act create the default issuing CA cert
    let workload_ca_cert = crypto.create_certificate(&edgelet_ca_props).unwrap();

    // assert (CA cert)
    let buffer = workload_ca_cert.pem().unwrap();
    assert!(!buffer.as_bytes().is_empty());
    let cn = workload_ca_cert.get_common_name().unwrap();
    assert_eq!("test-iotedge-cn".to_string(), cn);

    let workload_ca_cert = crypto
        .get_certificate(IOTEDGED_CA_ALIAS.to_string())
        .unwrap();
    let buffer = workload_ca_cert.pem().unwrap();
    assert!(!buffer.as_bytes().is_empty());
    let cn = workload_ca_cert.get_common_name().unwrap();
    assert_eq!("test-iotedge-cn".to_string(), cn);

    let san_entries: Vec<String> = vec![
        "URI: bar:://pity/foo".to_string(),
        "DNS: foo.bar".to_string(),
    ];

    // act
    let props = CertificateProperties::new(
        3600,
        "Common Name".to_string(),
        CertificateType::Client,
        "Alias".to_string(),
    )
    .with_san_entries(san_entries);

    let cert_info = crypto.create_certificate(&props).unwrap();

    assert!(cert_info.get_valid_to().is_ok());

    let buffer = cert_info.pem().unwrap();
    let cn = cert_info.get_common_name().unwrap();

    let pk = match cert_info.get_private_key().unwrap() {
        Some(pk) => pk,
        None => panic!("Expected to find a key"),
    };

    // assert
    assert!(!buffer.as_bytes().is_empty());
    match pk {
        PrivateKey::Ref(_) => panic!("did not expect reference private key"),
        PrivateKey::Key(KeyBytes::Pem(k)) => assert!(!k.as_bytes().is_empty()),
    }
    assert_eq!(cn, "Common Name".to_string());

    // cleanup
    crypto.destroy_certificate("Alias".to_string()).unwrap();
    crypto
        .destroy_certificate(IOTEDGED_CA_ALIAS.to_string())
        .unwrap();
}
