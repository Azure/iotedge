// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy::all, clippy::pedantic))]

extern crate edgelet_core;
extern crate edgelet_hsm;

use edgelet_core::{Certificate, GetTrustBundle};
use edgelet_hsm::Crypto;

#[test]
fn crypto_get_trust_bundle() {
    // arrange
    let crypto = Crypto::new().unwrap();

    // act
    let cert_info = crypto.get_trust_bundle().unwrap();

    let buffer = cert_info.pem().unwrap();

    if cert_info.get_private_key().unwrap().is_some() {
        panic!("do not expect to find a key");
    }

    // assert
    // assume cert_type is PEM(0)
    assert!(!buffer.as_bytes().is_empty());
}
