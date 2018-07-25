// Copyright (c) Microsoft. All rights reserved.
extern crate edgelet_core;
extern crate edgelet_hsm;

use edgelet_core::{CertificateProperties, CertificateType, CreateCertificate, CertificateIssuer, IOTEDGED_CA_ALIAS};
use edgelet_hsm::Crypto;

#[test]
fn crypto_create_cert_input_fail() {
    // arrange
    let crypto = Crypto::new().unwrap();

    let edgelet_ca_props = CertificateProperties::new(
        3600,
        "test-iotedge-cn".to_string(),
        CertificateType::Ca,
        IOTEDGED_CA_ALIAS.to_string(),
    ).with_issuer(CertificateIssuer::DeviceCa);

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
    match crypto.create_certificate(&props_time) {
        Ok(_) => panic!("Expected an error from bad time"),
        Err(_) => (),
    }
    match crypto.create_certificate(&props_cn) {
        Ok(_) => panic!("Expected an error from bad common name"),
        Err(_) => (),
    }
    match crypto.create_certificate(&props_type) {
        Ok(_) => panic!("Expected an error from bad cert type"),
        Err(_) => (),
    }
    match crypto.create_certificate(&props_a) {
        Ok(_) => panic!("Expected an error from bad alias"),
        Err(_) => (),
    }
    match crypto.destroy_certificate("unknown_cert_alias".to_string()) {
        Ok(_) => (),
        Err(_) => panic!("Expected no error when destroying a certificate that does not exist"),
    }

    // cleanup
    crypto.destroy_certificate(IOTEDGED_CA_ALIAS.to_string()).unwrap();
}
