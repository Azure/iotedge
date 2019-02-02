// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]

extern crate hsm;

use hsm::{GetCerts, X509};

fn main() {
    let hsm_x509 = X509::new().unwrap();
    println!("common name = {}", hsm_x509.get_common_name().unwrap());
}
