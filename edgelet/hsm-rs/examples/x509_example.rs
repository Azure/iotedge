// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use hsm::{GetDeviceIdentityCertificate, X509};

fn main() {
    let hsm_x509 = X509::new(1000).unwrap();
    println!("common name = {}", hsm_x509.get_common_name().unwrap());
}
