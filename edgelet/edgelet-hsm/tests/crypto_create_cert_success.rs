// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

extern crate edgelet_core;
extern crate edgelet_hsm;

use edgelet_core::{
    Certificate, CertificateIssuer, CertificateProperties, CertificateType, CreateCertificate,
    KeyBytes, PrivateKey, Signature, IOTEDGED_CA_ALIAS,
};
use edgelet_hsm::Crypto;

#[test]
fn crypto_create_cert_success() {
    // arrange
    let crypto = Crypto::new().unwrap();

    // create the default issuing CA cert properties
    let edgelet_ca_props = CertificateProperties::new(
        3600,
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

    let san_entries: Vec<String> = vec![
        "URI: bar:://pity/foo".to_string(),
        "DNS: foo.bar".to_string(),
    ];

    // act
    let props = CertificateProperties::new(
        3600,
        "Common Name".to_string(),
        CertificateType::Ca,
        "Alias".to_string(),
    )
    .with_san_entries(san_entries);

    let cert_info = crypto.create_certificate(&props).unwrap();

    assert!(cert_info.get_valid_to().is_ok());

    let buffer = cert_info.pem().unwrap();

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

    // cleanup
    crypto.destroy_certificate("Alias".to_string()).unwrap();
    crypto
        .destroy_certificate(IOTEDGED_CA_ALIAS.to_string())
        .unwrap();
}
