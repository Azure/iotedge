// Copyright (c) Microsoft. All rights reserved.
extern crate edgelet_core;
extern crate edgelet_hsm;

//use edgelet_core::{Signature, Certificate, PrivateKey, GetTrustBundle};
use edgelet_core::GetTrustBundle;
use edgelet_hsm::Crypto;

#[test]
fn crypto_get_trust_bundle() {
    // arrange
    let crypto = Crypto::default();

    // act
    let cert_info = crypto.get_trust_bundle(); //.unwrap();

    match cert_info {
        //assert
        Ok(_) => panic!("Decrypt function is not yet implemented, but got a good result"),
        Err(_) => (),
    };
    // let (cert_type, buffer) = cert_info.get().unwrap();

    // let (key_type, pk) = cert_info.get_private_key().unwrap();

    // // assert
    // // assume cert_type and key_type is PEM(0)
    // assert_eq!(0, cert_type);
    // assert!(buffer.as_bytes().len() > 0);
    // assert_eq!(0, key_type);
    // match pk {
    //     PrivateKey::Ref(_) => panic!("did not expect reference private key"),
    //     PrivateKey::Key(k) => assert!(k.as_bytes().len() > 0),
    // }
}
