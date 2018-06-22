// Copyright (c) Microsoft. All rights reserved.
extern crate edgelet_core;
extern crate edgelet_hsm;

use edgelet_core::{
    Certificate, CertificateProperties, CertificateType, CreateCertificate, KeyBytes, PrivateKey,
    Signature,
};
use edgelet_hsm::Crypto;

#[test]
fn crypto_create_cert_success() {
    // arrange
    let crypto = Crypto::new().unwrap();

    // act
    let props = CertificateProperties::new(
        3600,
        "Common Name".to_string(),
        CertificateType::Ca,
        "Alias".to_string(),
    );

    let cert_info = crypto.create_certificate(&props).unwrap();

    let buffer = cert_info.pem().unwrap();

    let pk = match cert_info.get_private_key().unwrap() {
        Some(pk) => pk,
        None => panic!("Expected to find a key"),
    };

    // assert
    assert!(buffer.as_bytes().len() > 0);
    match pk {
        PrivateKey::Ref(_) => panic!("did not expect reference private key"),
        PrivateKey::Key(KeyBytes::Pem(k)) => assert!(k.as_bytes().len() > 0),
    }
}
