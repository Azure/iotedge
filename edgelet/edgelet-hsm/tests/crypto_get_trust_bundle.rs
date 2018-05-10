// Copyright (c) Microsoft. All rights reserved.
extern crate edgelet_core;
extern crate edgelet_hsm;

use edgelet_core::{Certificate, GetTrustBundle};
use edgelet_hsm::Crypto;

#[test]
fn crypto_get_trust_bundle() {
    // arrange
    // todo
    // temporarily execute this test only when env variable EDGEHOMEDIR
    // is set with valid certificates
    if std::env::var("EDGEHOMEDIR").is_ok() {
        let crypto = Crypto::default();

        // act
        let cert_info = crypto.get_trust_bundle().unwrap();

        let buffer = cert_info.pem().unwrap();

        match cert_info.get_private_key().unwrap() {
            Some(_) => panic!("do not expect to find a key"),
            None => (),
        };

        // assert
        // assume cert_type is PEM(0)
        assert!(buffer.as_bytes().len() > 0);
    }
}
