// Copyright (c) Microsoft. All rights reserved.
extern crate edgelet_core;
extern crate edgelet_hsm;

use edgelet_core::{Certificate, CertificateProperties, CertificateType, CreateCertificate,
                   PrivateKey, Signature};
use edgelet_hsm::Crypto;

#[test]
fn crypto_create_cert_success() {
    // arrange
    let crypto = Crypto::default();

    // act
    let props = CertificateProperties::new(
        3600,
        "Common Name".to_string(),
        CertificateType::Ca,
        "Issuer Alias".to_string(),
        "Alias".to_string(),
    );

    let cert_info = crypto.create_certificate(&props).unwrap();

    let buffer = cert_info.pem().unwrap();

    let (key_type, pk) = cert_info.get_private_key().unwrap();

    // assert
    // assume cert_type and key_type is PEM(0)
    assert!(buffer.as_bytes().len() > 0);
    assert_eq!(0, key_type);
    match pk {
        PrivateKey::Ref(_) => panic!("did not expect reference private key"),
        PrivateKey::Key(k) => assert!(k.as_bytes().len() > 0),
    }
}
