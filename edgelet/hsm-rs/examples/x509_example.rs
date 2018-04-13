// Copyright (c) Microsoft. All rights reserved.
extern crate hsm;

use hsm::{GetCerts, X509};

fn main() {
    let hsm_x509 = X509::new();
    println!("common name = {}", hsm_x509.get_common_name().unwrap());
}
